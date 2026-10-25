using System.Collections.Generic;
using Okey101.Core.Model;
using Okey101.Core.Turns;

namespace Okey101.Core.Rules
{
    public static class TableEvaluator
    {
        /// <summary>
        /// spec/22 elden bitiş: a player who has not opened may lay everything but the last tile without reaching 101 / 5
        /// pairs and finish with that discard (the ×2 "opened this turn" finish). One rule for the engine, bots and finders.
        /// </summary>
        public static bool FinishesFromHand(int handCount, int laidCount) => handCount >= 15 && handCount - laidCount == 1;

        /// <summary>
        /// The series total or pair count <paramref name="player"/>'s opening must reach: 101 / 5, and at a katlamalı table
        /// one more than the highest opening of the same kind so far this hand (series and pairs bars are independent). With
        /// <see cref="RuleOptions.PartnerOpeningsRaise"/> off the partner's (opposite seat's) openings do not count.
        /// </summary>
        public static int OpeningBar(RoundState state, int player, OpeningKind kind)
            => OpeningBar(state.Options, player, kind, other => state.Players[other].Opening, other => state.Players[other].OpeningValue);

        /// <summary>The bar for the view's own seat, from the public openings (what a client shows on Aç / Çift aç).</summary>
        public static int OpeningBar(PlayerView view, OpeningKind kind)
            => OpeningBar(view.Options, view.Actor, kind, other => view.Players[other].Opening, other => view.Players[other].OpeningValue);

        private static int OpeningBar(RuleOptions options, int player, OpeningKind kind, System.Func<int, OpeningKind> opening, System.Func<int, int> value)
        {
            var bar = kind == OpeningKind.Pairs ? 5 : 101;
            if (!options.Doubling) return bar;
            for (var other = 0; other < 4; other++)
            {
                if (other == player || (!options.PartnerOpeningsRaise && other == (player + 2) % 4)) continue;
                if (opening(other) == kind && value(other) >= bar) bar = value(other) + 1;
            }
            return bar;
        }

        /// <summary>
        /// Identifies the active player's position for a search's visited set: opening, series budget, the hand as a tile
        /// set, and each table meld as its tile set plus what its okeys stand for. Order-free (a meld grown at either end
        /// is the same meld) and one string per call — planners key every state they reach.
        /// </summary>
        public static string PositionKey(RoundState state)
        {
            var player = state.Players[state.ActivePlayer];
            var indicator = TileCatalog.Create()[state.Indicator.Value].PrintedFace.Value;
            var key = new System.Text.StringBuilder(64 + state.Table.Count * 24).Append((int)player.Opening).Append('/').Append(state.SeriesTilesUsed).Append(':');
            AppendSet(key, player.Hand);
            for (var m = 0; m < state.Table.Count; m++)
            {
                var meld = state.Table[m]; var tiles = meld.Group.Tiles;
                key.Append('|').Append(meld.Id).Append('/').Append(meld.Owner).Append('/').Append((int)meld.Group.Kind).Append(':');
                ulong low = 0, high = 0; PlacedTile? first = null, second = null;
                for (var i = 0; i < tiles.Count; i++)
                {
                    var id = tiles[i].Id.Value; if (id < 64) low |= 1UL << id; else high |= 1UL << (id - 64);
                    if (!TileCatalog.IsRealJoker(tiles[i].Id, indicator)) continue;
                    if (first == null) first = tiles[i]; else second = tiles[i];
                }
                key.Append(low).Append('.').Append(high);
                if (second.HasValue && second.Value.Id.Value < first.Value.Id.Value) { var swap = first; first = second; second = swap; }
                foreach (var joker in new[] { first, second })
                    if (joker.HasValue) key.Append('j').Append(joker.Value.Id.Value).Append('=').Append((int)joker.Value.RepresentedFace.Color).Append('/').Append(joker.Value.RepresentedFace.Number);
            }
            return key.ToString();
        }

        private static void AppendSet(System.Text.StringBuilder key, IReadOnlyList<TileId> tiles)
        {
            ulong low = 0, high = 0;
            for (var i = 0; i < tiles.Count; i++) { var id = tiles[i].Value; if (id < 64) low |= 1UL << id; else high |= 1UL << (id - 64); }
            key.Append(low).Append('.').Append(high);
        }

        /// <summary>Applies ordered table actions atomically without discarding or advancing the turn.</summary>
        public static RuleResult<RoundState> Evaluate(RoundState drawnState, IReadOnlyList<TableAction> actions)
        {
            if (drawnState == null || actions == null)
                return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);
            if (drawnState.Phase != RoundPhase.PlanningTurn)
                return RuleResult<RoundState>.Fail(ErrorCode.WrongPhase);
            if (drawnState.Result != null || !StateInvariant.Validate(drawnState).Success)
                return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);

            var indicator = TileCatalog.Create()[drawnState.Indicator.Value].PrintedFace.Value;
            foreach (var meld in drawnState.Table)
                if (!MeldValidator.Validate(meld.Group, indicator).Success)
                    return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);
            if (drawnState.Players[drawnState.ActivePlayer].Opening == OpeningKind.Pairs && drawnState.SeriesTilesUsed > 2)
                return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);

            var candidate = new Candidate(drawnState, indicator);
            foreach (var action in actions)
            {
                var error = candidate.Apply(action);
                if (error != ErrorCode.None) return RuleResult<RoundState>.Fail(error);
            }
            return RuleResult<RoundState>.Ok(candidate.Build());
        }

        private sealed class Candidate
        {
            private readonly RoundState source;
            private readonly TileFace indicator;
            private readonly List<TileId> hand;
            private readonly List<Meld> table;
            private OpeningKind opening;
            private int openingValue;
            private int seriesTilesUsed;
            private long nextMeldId;
            private readonly int[] ownerPenalties = new int[4];

            public Candidate(RoundState source, TileFace indicator)
            {
                this.source = source;
                this.indicator = indicator;
                hand = new List<TileId>(source.Players[source.ActivePlayer].Hand);
                table = new List<Meld>(source.Table);
                opening = source.Players[source.ActivePlayer].Opening;
                openingValue = source.Players[source.ActivePlayer].OpeningValue;
                seriesTilesUsed = source.SeriesTilesUsed;
                foreach (var meld in table)
                    if (meld.Id >= nextMeldId) nextMeldId = (long)meld.Id + 1;
            }

            public ErrorCode Apply(TableAction action)
            {
                switch (action)
                {
                    case OpenGroups open: return Open(open);
                    case AddGroup add: return Add(add.Group);
                    case ExtendMeld extend: return source.Options.Processing ? Extend(extend) : ErrorCode.ProcessingLimit; // işleme switched off
                    case SwapJoker swap: return Swap(swap);
                    default: return ErrorCode.InvalidState;
                }
            }

            private ErrorCode Open(OpenGroups action)
            {
                if (action.Opening == OpeningKind.None || action.Groups.Count == 0)
                    return ErrorCode.InvalidOpening;
                if (opening != OpeningKind.None && opening != action.Opening)
                    return ErrorCode.OpeningTypeLocked;

                var selected = new HashSet<TileId>();
                var total = 0;
                foreach (var group in action.Groups)
                {
                    if ((group.Kind == MeldKind.Pair) != (action.Opening == OpeningKind.Pairs))
                        return ErrorCode.InvalidGroup;
                    var validation = MeldValidator.Validate(group, indicator);
                    if (!validation.Success) return validation.Error;
                    foreach (var tile in group.Tiles)
                        if (!hand.Contains(tile.Id) || !selected.Add(tile.Id)) return ErrorCode.InvalidTile;
                    total += validation.Value;
                }
                bool finishingFromHand = FinishesFromHand(hand.Count, selected.Count);
                var reached = action.Opening == OpeningKind.Series ? total : action.Groups.Count;
                if (opening == OpeningKind.None && !finishingFromHand && reached < OpeningBar(source, source.ActivePlayer, action.Opening))
                    return ErrorCode.InvalidOpening;
                if (nextMeldId + action.Groups.Count - 1 > int.MaxValue) return ErrorCode.InvalidState;

                foreach (var id in selected) hand.Remove(id);
                foreach (var group in action.Groups)
                    table.Add(new Meld((int)nextMeldId++, source.ActivePlayer, group));
                // The opening value is the first opening's: later groups (yere aç) and işleme never change it.
                if (opening == OpeningKind.None) openingValue = reached;
                opening = action.Opening;
                return ErrorCode.None;
            }

            private ErrorCode Add(MeldProposal group)
            {
                if (opening == OpeningKind.None) return ErrorCode.OpeningRequired;
                if (group.Kind != MeldKind.Pair && opening == OpeningKind.Pairs)
                    return ErrorCode.OpeningTypeLocked;
                if (group.Kind == MeldKind.Pair && opening == OpeningKind.Series && !HasPairsPlayer())
                    return ErrorCode.OpeningRequired;
                var validation = MeldValidator.Validate(group, indicator);
                if (!validation.Success) return validation.Error;
                foreach (var tile in group.Tiles)
                    if (!hand.Contains(tile.Id)) return ErrorCode.InvalidTile;
                if (nextMeldId > int.MaxValue) return ErrorCode.InvalidState;

                foreach (var tile in group.Tiles) hand.Remove(tile.Id);
                table.Add(new Meld((int)nextMeldId++, source.ActivePlayer, group));
                return ErrorCode.None;
            }

            private bool HasPairsPlayer()
            {
                if (opening == OpeningKind.Pairs) return true;
                for (var player = 0; player < source.Players.Count; player++)
                    if (player != source.ActivePlayer && source.Players[player].Opening == OpeningKind.Pairs)
                        return true;
                return false;
            }

            private ErrorCode Extend(ExtendMeld action)
            {
                if (opening == OpeningKind.None) return ErrorCode.OpeningRequired;
                var index = table.FindIndex(m => m.Id == action.MeldId);
                if (index < 0 || action.Tiles.Count == 0) return ErrorCode.InvalidGroup;
                var target = table[index];
                if (target.Group.Kind == MeldKind.Pair) return ErrorCode.InvalidGroup;

                var selected = new HashSet<TileId>();
                foreach (var tile in action.Tiles)
                    if (!hand.Contains(tile.Id) || !selected.Add(tile.Id)) return ErrorCode.InvalidTile;

                if (target.Group.Kind == MeldKind.Run)
                {
                    var minimum = 13;
                    var maximum = 1;
                    foreach (var tile in target.Group.Tiles)
                    {
                        var number = tile.RepresentedFace.Number;
                        if (number < minimum) minimum = number;
                        if (number > maximum) maximum = number;
                    }
                    foreach (var tile in action.Tiles)
                        if (action.Prepend ? tile.RepresentedFace.Number >= minimum : tile.RepresentedFace.Number <= maximum)
                            return ErrorCode.InvalidGroup;
                }

                var tiles = new List<PlacedTile>(target.Group.Tiles);
                tiles.AddRange(action.Tiles);
                if (target.Group.Kind == MeldKind.Run)
                    tiles.Sort((left, right) => left.RepresentedFace.Number.CompareTo(right.RepresentedFace.Number));
                var group = new MeldProposal(target.Group.Kind, tiles);
                var validation = MeldValidator.Validate(group, indicator);
                if (!validation.Success) return validation.Error;
                var budgetError = UseSeriesBudget(action.Tiles.Count);
                if (budgetError != ErrorCode.None) return budgetError;

                foreach (var tile in action.Tiles) hand.Remove(tile.Id);
                table[index] = new Meld(target.Id, target.Owner, group);
                return ErrorCode.None;
            }

            private ErrorCode Swap(SwapJoker action)
            {
                if (opening == OpeningKind.None) return ErrorCode.OpeningRequired;
                var index = table.FindIndex(m => m.Id == action.MeldId);
                if (index < 0) return ErrorCode.InvalidGroup;
                if (!hand.Contains(action.ReplacementId)) return ErrorCode.InvalidTile;
                if (!TileCatalog.IsRealJoker(action.JokerId, indicator) ||
                    TileCatalog.IsRealJoker(action.ReplacementId, indicator))
                    return ErrorCode.InvalidReplacement;

                var target = table[index];
                var tiles = new List<PlacedTile>(target.Group.Tiles);
                var jokerIndex = tiles.FindIndex(t => t.Id == action.JokerId);
                if (jokerIndex < 0 || TileCatalog.GetNonJokerFace(action.ReplacementId, indicator) != tiles[jokerIndex].RepresentedFace)
                    return ErrorCode.InvalidReplacement;
                if (target.Group.Kind != MeldKind.Pair)
                {
                    var budgetError = UseSeriesBudget(1);
                    if (budgetError != ErrorCode.None) return budgetError;
                }

                // The owner's +101 must fit an int (saturated totals are refused like the other counters, not thrown).
                if (target.Owner != source.ActivePlayer && source.Players[target.Owner].Penalties > int.MaxValue - Penalties.Standard - ownerPenalties[target.Owner])
                    return ErrorCode.InvalidState;
                tiles[jokerIndex] = new PlacedTile(action.ReplacementId, tiles[jokerIndex].RepresentedFace);
                table[index] = new Meld(target.Id, target.Owner, new MeldProposal(target.Group.Kind, tiles));
                hand.Remove(action.ReplacementId);
                hand.Add(action.JokerId);
                // spec/17 §3: whoever laid the okey pays +101 when another player takes it.
                if (target.Owner != source.ActivePlayer) ownerPenalties[target.Owner] += Penalties.Standard;
                return ErrorCode.None;
            }

            private ErrorCode UseSeriesBudget(int count)
            {
                if (opening == OpeningKind.Pairs && count > 2 - seriesTilesUsed) return ErrorCode.ProcessingLimit;
                if (count > int.MaxValue - seriesTilesUsed) return ErrorCode.InvalidState;
                seriesTilesUsed += count;
                return ErrorCode.None;
            }

            public RoundState Build()
            {
                var players = new PlayerState[4];
                for (var player = 0; player < players.Length; player++)
                    players[player] = ownerPenalties[player] == 0 ? source.Players[player]
                        : new PlayerState(source.Players[player].Hand, source.Players[player].Opening, source.Players[player].Penalties + ownerPenalties[player], source.Players[player].OpeningValue);
                players[source.ActivePlayer] = new PlayerState(hand, opening, players[source.ActivePlayer].Penalties, openingValue);
                return new RoundState(source.RoundId, source.Version, source.Seed, source.StartingPlayer,
                    source.ActivePlayer, source.TurnNumber, source.Phase, source.Indicator, source.Stock,
                    players, table, source.Discards, source.PendingDraw, source.OpeningAtTurnStart,
                    seriesTilesUsed, source.IsInitialTurn, source.LastAcceptedSequences, source.Result, source.PreviousDiscardReturned, source.Options, source.Deal);
            }
        }
    }
}
