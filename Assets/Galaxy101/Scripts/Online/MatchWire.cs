using System;
using System.Collections.Generic;
using System.Linq;
using Galaxy101.Contracts;
using Okey101.Core.Model;

namespace Galaxy101.Online
{
    /// <summary>
    /// The client side of the server's Wire translation: engine commands → <see cref="MatchCommandMessage"/>, and a
    /// <see cref="PlayerViewDto"/> → <see cref="PlayerView"/> turned so that this seat is seat 0 (the table always shows me
    /// at the bottom). <see cref="ToSeat"/> / <see cref="FromSeat"/> convert between absolute and local seats.
    /// </summary>
    public sealed class MatchWire
    {
        public int YourSeat { get; }
        public MatchWire(int yourSeat) { YourSeat = yourSeat; }

        /// <summary>Absolute seat → table seat (0 = me, 1 = right, 2 = across, 3 = left).</summary>
        public int FromSeat(int absolute) => ((absolute - YourSeat) % 4 + 4) % 4;
        public int ToSeat(int local) => (local + YourSeat) % 4;

        // ---------- outbound ----------
        public static MatchCommandMessage ToMessage(GameCommand command, Guid matchId, long expectedVersion)
        {
            MatchCommandMessage m;
            switch (command)
            {
                case DrawCommand d: m = new MatchCommandMessage { Kind = "draw", Source = d.Source == DrawSource.Stock ? "stock" : "discard" }; break;
                case ReturnDiscardCommand _: m = new MatchCommandMessage { Kind = "return" }; break;
                case ShortOpeningCommand s: m = new MatchCommandMessage { Kind = "shortOpening", Opening = Name(s.Opening) }; break;
                case CommitTurnCommand c: m = new MatchCommandMessage { Kind = "commit", Plan = ToDto(c.Plan) }; break;
                default: throw new ArgumentException("Unknown command.");
            }
            m.MatchId = matchId; m.ExpectedVersion = expectedVersion; return m;
        }

        public static TurnPlanDto ToDto(TurnPlan plan) => new TurnPlanDto
        {
            Discard = plan.Discard.Value, AcceptPenalty = plan.AcceptPenalty,
            Actions = plan.Actions.Select(a =>
            {
                switch (a)
                {
                    case OpenGroups o: return new TableActionDto { Type = "open", Opening = Name(o.Opening), Groups = o.Groups.Select(g => ToDto(g)).ToList() };
                    case AddGroup g: return new TableActionDto { Type = "add", Group = ToDto(g.Group) };
                    case ExtendMeld e: return new TableActionDto { Type = "extend", MeldId = e.MeldId, Prepend = e.Prepend, Tiles = e.Tiles.Select(ToDto).ToList() };
                    case SwapJoker s: return new TableActionDto { Type = "swap", MeldId = s.MeldId, JokerId = s.JokerId.Value, ReplacementId = s.ReplacementId.Value };
                    default: throw new ArgumentException("Unknown action.");
                }
            }).ToList(),
        };

        public static MeldDto ToDto(MeldProposal g, int id = 0, int owner = 0) => new MeldDto
        {
            Id = id, Owner = owner, Kind = g.Kind == MeldKind.Run ? "run" : g.Kind == MeldKind.Set ? "set" : "pair", Tiles = g.Tiles.Select(ToDto).ToList(),
        };

        private static PlacedTileDto ToDto(PlacedTile t) => new PlacedTileDto { Id = t.Id.Value, Color = Name(t.RepresentedFace.Color), Number = t.RepresentedFace.Number };

        // ---------- inbound ----------
        public PlayerView ToView(PlayerViewDto v)
        {
            var players = new PublicPlayerInfo[4]; var discards = new IReadOnlyList<TileId>[4];
            for (int abs = 0; abs < 4; abs++)
            {
                int k = FromSeat(abs);
                var p = v.Players[abs]; players[k] = new PublicPlayerInfo(p.HandCount, Opening(p.Opening), p.Penalties);
                discards[k] = (v.Discards.Count > abs ? v.Discards[abs] : new List<int>()).Select(id => new TileId(id)).ToArray();
            }
            // The own entry carries the real hand size (the engine's projection lists it too).
            var table = v.Table.Select(m => new Meld(m.Id, FromSeat(m.Owner), ToProposal(m))).ToArray();
            var pending = v.PendingDraw == null || FromSeat(v.ActiveSeat) != 0 ? null
                : new DrawReceipt(v.PendingDraw.Source == "stock" ? DrawSource.Stock : DrawSource.PreviousDiscard, new TileId(v.PendingDraw.Tile), v.PendingDraw.WasLastStock);
            DealInfo deal = v.Deal == null ? null : new DealInfo(FromSeat(v.Deal.Dealer), v.Deal.DieA, v.Deal.DieB, v.Deal.IndicatorStack, v.Deal.IndicatorPosition, v.Deal.FakeOkeyShown, v.Deal.ExtraStack);
            return new PlayerView(v.RoundId, v.Version, 0, FromSeat(v.ActiveSeat), Phase(v.Phase), v.Hand.Select(id => new TileId(id)).ToArray(), Opening(v.Opening), v.Penalties,
                0, new TileId(v.Indicator), Math.Min(105, v.StockCount), players, table, discards, pending, v.SeriesTilesUsed, v.Result == null ? null : ToResult(v.Result), deal,
                v.Rules == null ? RuleOptions.Standard : new RuleOptions(v.Rules.OkeyPenalty, v.Rules.PlayablePenalty));
        }

        public RoundResult ToResult(RoundResultDto r)
        {
            var reason = r.Reason == "discardWin" ? FinishReason.DiscardWin : r.Reason == "stockExhausted" ? FinishReason.StockExhausted : FinishReason.AllPairs;
            int? winner = r.Winner.HasValue ? FromSeat(r.Winner.Value) : (int?)null;
            var finish = reason == FinishReason.DiscardWin
                ? new FinishInfo(reason, winner, r.WinnerOpening == "pairs" ? OpeningKind.Pairs : OpeningKind.Series, r.WinnerOpenedThisTurn, r.DiscardedRealJoker)
                : new FinishInfo(reason, null, OpeningKind.None, false, false);
            var scores = new ScoreBreakdown[4];
            for (int abs = 0; abs < 4; abs++)
            {
                var s = r.Scores.Count > abs ? r.Scores[abs] : new ScoreDto { M = 1 };
                scores[FromSeat(abs)] = new ScoreBreakdown(s.HasScore, s.N, s.J, s.P, s.B, s.M == 0 ? 1 : s.M, s.Total);
            }
            return new RoundResult(finish, scores);
        }

        /// <summary>The server's "rule.wrongPlayer" style codes as engine errors.</summary>
        public static ErrorCode Error(string code)
        {
            if (string.IsNullOrEmpty(code)) return ErrorCode.InvalidState;
            string name = code.StartsWith("rule.") ? code.Substring(5) : code;
            foreach (ErrorCode e in Enum.GetValues(typeof(ErrorCode)))
                if (string.Equals(e.ToString(), name, StringComparison.OrdinalIgnoreCase)) return e == ErrorCode.None ? ErrorCode.InvalidState : e;
            return code == "timeout" || code == "offline" ? ErrorCode.Cancelled : ErrorCode.InvalidState;
        }

        private static MeldProposal ToProposal(MeldDto m) => new MeldProposal(m.Kind == "run" ? MeldKind.Run : m.Kind == "set" ? MeldKind.Set : MeldKind.Pair,
            m.Tiles.Select(t => new PlacedTile(new TileId(t.Id), new TileFace(Color(t.Color), t.Number))).ToArray());

        private static OpeningKind Opening(string value) => value == "series" ? OpeningKind.Series : value == "pairs" ? OpeningKind.Pairs : OpeningKind.None;
        private static RoundPhase Phase(string value) => value == "awaitingDraw" ? RoundPhase.AwaitingDraw : value == "planningTurn" ? RoundPhase.PlanningTurn : value == "finished" ? RoundPhase.Finished : RoundPhase.Cancelled;
        private static TileColor Color(string value) => value == "red" ? TileColor.Red : value == "yellow" ? TileColor.Yellow : value == "blue" ? TileColor.Blue : TileColor.Black;
        public static string Name(OpeningKind o) => o == OpeningKind.Series ? "series" : o == OpeningKind.Pairs ? "pairs" : "none";
        private static string Name(TileColor c) => c == TileColor.Red ? "red" : c == TileColor.Yellow ? "yellow" : c == TileColor.Blue ? "blue" : "black";
    }
}
