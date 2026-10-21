using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Okey101.Core.Model;
using Okey101.Core.Rules;
using Okey101.Core.Scoring;
using Okey101.Core.Turns;

namespace Okey101.Bots
{
    public sealed class DrawDecision
    {
        public DrawSource Source { get; }
        public TurnPlan Witness { get; }
        public DrawDecision(DrawSource source, TurnPlan witness = null)
        {
            if (source != DrawSource.Stock && source != DrawSource.PreviousDiscard) throw new ArgumentOutOfRangeException(nameof(source));
            if ((source == DrawSource.PreviousDiscard) != (witness != null)) throw new ArgumentException("A previous-discard decision requires its legal witness; stock must not carry one.");
            Source = source; Witness = witness;
        }
    }

    public sealed class BotPlanner
    {
        private const int NodeBudget = 4096;
        public DrawDecision ChooseDraw(PlayerView view, CancellationToken token)
        {
            Validate(view, RoundPhase.AwaitingDraw, token);
            if (view.Discards[(view.Actor + 3) % 4].Count == 0) return new DrawDecision(DrawSource.Stock);
            var witness = DiscardTurnFinder.Find(view, token);
            if (!witness.Success)
            {
                if (witness.Error == ErrorCode.Cancelled) throw new OperationCanceledException(token);
                if (witness.Error == ErrorCode.NoLegalUse || witness.Error == ErrorCode.PreviousDiscardUnavailable) return new DrawDecision(DrawSource.Stock);
                throw new ArgumentException("Invalid bot observation: " + witness.Error, nameof(view));
            }
            var picked = view.Discards[(view.Actor + 3) % 4].Last();
            var drawn = ChangeDraw(view, true, picked);
            var side = new Search(State(drawn), token, witness.Value, NodeBudget / 2).Run();
            // The stock's actual next face is unknown. Use the current known hand as a
            // conservative baseline; never simulate a canonically allocated stock tile.
            var baseline = new Search(State(PlanningWithoutDraw(view)), token, null, NodeBudget / 2).Run();
            if (side.Rank.Winning || side.Rank.CompareValue(baseline.Rank) < 0)
                return new DrawDecision(DrawSource.PreviousDiscard, side.Plan);
            return new DrawDecision(DrawSource.Stock);
        }
        public TurnPlan PlanTurn(PlayerView view, CancellationToken token) => PlanTurn(view, null, token);
        public TurnPlan PlanTurn(PlayerView view, TurnPlan witness, CancellationToken token)
        {
            Validate(view, RoundPhase.PlanningTurn, token);
            if (view.PendingDraw?.Source == DrawSource.PreviousDiscard && witness == null)
            {
                var beforeDraw = ChangeDraw(view, false, view.PendingDraw.Tile);
                var found = DiscardTurnFinder.Find(beforeDraw, token);
                if (found.Error == ErrorCode.Cancelled) throw new OperationCanceledException(token);
                if (!found.Success) throw new InvalidOperationException("A committed previous discard has no legal continuation: " + found.Error);
                witness = found.Value;
            }
            return new Search(State(view), token, witness).Run().Plan;
        }
        private static void Validate(PlayerView view, RoundPhase phase, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (view == null) throw new ArgumentNullException(nameof(view));
            if (view.Actor != view.ActivePlayer || view.Phase != phase || view.Result != null)
                throw new ArgumentException("Bot requires its own active, non-terminal turn in the requested phase.", nameof(view));
        }
        private static RoundState State(PlayerView view)
        {
            var result = PlayerViewProjector.ForEvaluation(view);
            if (!result.Success) throw new ArgumentException("Invalid bot observation: " + result.Error, nameof(view));
            return result.Value;
        }
        private static PlayerView PlanningWithoutDraw(PlayerView view)
            => new PlayerView(view.RoundId, view.Version, view.Actor, view.ActivePlayer, RoundPhase.PlanningTurn, view.OwnHand,
                view.OwnOpening, view.OwnPenalties, view.OwnLastSequence, view.Indicator, view.StockCount, view.Players, view.Table,
                view.Discards, null, view.SeriesTilesUsed);
        private static PlayerView ChangeDraw(PlayerView view, bool add, TileId tile)
        {
            var hand = view.OwnHand.ToList(); var piles = view.Discards.ToArray(); var previous = (view.Actor + 3) % 4;
            if (add) { hand.Add(tile); piles[previous] = piles[previous].Take(piles[previous].Count - 1).ToArray(); }
            else { if (!hand.Remove(tile)) throw new ArgumentException("Drawn tile is absent."); piles[previous] = piles[previous].Concat(new[] { tile }).ToArray(); }
            var players = view.Players.ToArray(); players[view.Actor] = new PublicPlayerInfo(hand.Count, view.OwnOpening, view.OwnPenalties);
            return new PlayerView(view.RoundId, view.Version, view.Actor, view.ActivePlayer, add ? RoundPhase.PlanningTurn : RoundPhase.AwaitingDraw,
                hand, view.OwnOpening, view.OwnPenalties, view.OwnLastSequence, view.Indicator, view.StockCount, players, view.Table, piles,
                add ? new DrawReceipt(DrawSource.PreviousDiscard, tile, false) : null, view.SeriesTilesUsed);
        }
        private sealed class RankedPlan
        {
            public TurnPlan Plan;
            public Rank Rank;
        }
        private readonly struct Rank
        {
            public readonly bool Winning;
            private readonly int value, count, penalty;
            private readonly string canonical;
            public Rank(bool winning, int value, int count, int penalty, string canonical)
            { Winning = winning; this.value = value; this.count = count; this.penalty = penalty; this.canonical = canonical; }
            public int CompareValue(Rank other)
            {
                var compared = other.Winning.CompareTo(Winning); if (compared != 0) return compared;
                compared = value.CompareTo(other.value); if (compared != 0) return compared;
                compared = count.CompareTo(other.count); return compared != 0 ? compared : penalty.CompareTo(other.penalty);
            }
            public int Compare(Rank other) { var compared = CompareValue(other); return compared != 0 ? compared : string.CompareOrdinal(canonical, other.canonical); }
        }
        private sealed class Search
        {
            private static readonly FinishInfo HeuristicFinish = new FinishInfo(FinishReason.StockExhausted, null, OpeningKind.None, false, false);
            private readonly RoundState root;
            private readonly TileFace indicator;
            private readonly CancellationToken token;
            private readonly HashSet<string> visited = new HashSet<string>();
            private readonly TurnPlan witness;
            private readonly int budget;
            private RankedPlan best;
            private int nodes;
            public Search(RoundState root, CancellationToken token, TurnPlan witness, int budget = NodeBudget)
            { this.root = root; this.token = token; this.witness = witness; this.budget = budget; indicator = TileCatalog.Create()[root.Indicator.Value].PrintedFace.Value; }
            public RankedPlan Run()
            {
                token.ThrowIfCancellationRequested();
                var valid = TableEvaluator.Evaluate(root, Array.Empty<TableAction>());
                if (!valid.Success) throw new ArgumentException("Invalid planning observation: " + valid.Error);
                if (witness != null)
                {
                    var prefix = TableEvaluator.Evaluate(root, witness.Actions);
                    if (!prefix.Success || !TurnEvaluator.Evaluate(root, witness).Success) throw new ArgumentException("The preserved witness is invalid for this draw.");
                    Consider(prefix.Value, witness.Actions, witness.Discard);
                }
                Consider(root, Array.Empty<TableAction>());
                var pool = Groups(root.Players[root.ActivePlayer].Hand);
                if (root.Players[root.ActivePlayer].Opening == OpeningKind.None)
                {
                    Open(OpeningKind.Series, pool, budget / 2);
                    Open(OpeningKind.Pairs, pool, budget);
                }
                else Explore(root, new List<TableAction>(), pool, budget);
                token.ThrowIfCancellationRequested();
                return best ?? throw new InvalidOperationException("No legal bot turn exists for this observation.");
            }
            private bool Visit(int ceiling)
            { token.ThrowIfCancellationRequested(); if (nodes >= ceiling) return false; nodes++; return true; }
            private IReadOnlyList<MeldProposal> Groups(IReadOnlyList<TileId> hand)
            {
                var result = new List<MeldProposal>();
                foreach (var kind in new[] { MeldKind.Run, MeldKind.Set, MeldKind.Pair })
                    result.AddRange(MeldCandidateGenerator.Generate(hand, indicator, kind, token));
                return result.OrderByDescending(g => g.Tiles.Count).ThenByDescending(g => g.Tiles.Sum(t => t.RepresentedFace.Number))
                    .ThenBy(GroupKey, StringComparer.Ordinal).ToArray();
            }
            private void Open(OpeningKind opening, IReadOnlyList<MeldProposal> pool, int ceiling)
            {
                var hand = root.Players[root.ActivePlayer].Hand;
                // Optimistic face sum: if even both jokers at 13 cannot reach 101, do not
                // spend the series branch's budget enumerating impossible subsets.
                if (opening == OpeningKind.Series && hand.Sum(id => TileCatalog.IsRealJoker(id, indicator) ? 13 : TileCatalog.GetNonJokerFace(id, indicator).Number) < 101) return;
                var positions = hand.Select((id, index) => (Id: id, Index: index)).ToDictionary(item => item.Id, item => item.Index);
                var groups = pool.Where(g => (g.Kind == MeldKind.Pair) == (opening == OpeningKind.Pairs))
                    .Select(g => (Group: g, Mask: g.Tiles.Aggregate(0u, (mask, tile) => mask | (1u << positions[tile.Id])), Total: g.Tiles.Sum(t => t.RepresentedFace.Number))).ToArray();
                var selected = new List<MeldProposal>();
                void Select(int start, uint used, int total)
                {
                    if (!Visit(ceiling)) return;
                    if (opening == OpeningKind.Pairs ? selected.Count >= 5 : total >= 101)
                    {
                        var action = new OpenGroups(opening, selected);
                        var applied = TableEvaluator.Evaluate(root, new[] { action });
                        if (applied.Success && applied.Value.Players[root.ActivePlayer].Hand.Count > 0)
                            Explore(applied.Value, new List<TableAction> { action }, pool, ceiling);
                        return;
                    }
                    for (var i = start; i < groups.Length && nodes < ceiling; i++)
                    {
                        token.ThrowIfCancellationRequested(); var candidate = groups[i];
                        if ((used & candidate.Mask) != 0 || (used | candidate.Mask) == (1u << hand.Count) - 1) continue;
                        selected.Add(candidate.Group); Select(i + 1, used | candidate.Mask, total + candidate.Total); selected.RemoveAt(selected.Count - 1);
                    }
                }
                Select(0, 0, 0);
            }
            private void Explore(RoundState state, List<TableAction> path, IReadOnlyList<MeldProposal> pool, int ceiling)
            {
                if (!Visit(ceiling)) return;
                Consider(state, path);
                var hand = state.Players[state.ActivePlayer].Hand;
                if (hand.Count <= 1 || !visited.Add(StateKey(state))) return;
                foreach (var action in Actions(state, pool))
                {
                    if (nodes >= ceiling) break;
                    token.ThrowIfCancellationRequested();
                    var applied = TableEvaluator.Evaluate(state, new[] { action });
                    if (!applied.Success || applied.Value.Players[state.ActivePlayer].Hand.Count == 0) continue;
                    path.Add(action);
                    Explore(applied.Value, path, action is SwapJoker ? Groups(applied.Value.Players[state.ActivePlayer].Hand) : pool, ceiling);
                    path.RemoveAt(path.Count - 1);
                }
            }
            private IEnumerable<TableAction> Actions(RoundState state, IReadOnlyList<MeldProposal> pool)
            {
                var player = state.Players[state.ActivePlayer]; var hand = new HashSet<TileId>(player.Hand);
                foreach (var group in pool)
                    if (group.Tiles.Count < hand.Count && group.Tiles.All(t => hand.Contains(t.Id)) &&
                        (group.Kind == MeldKind.Pair ? player.Opening == OpeningKind.Pairs || state.Players.Any(p => p.Opening == OpeningKind.Pairs) : player.Opening == OpeningKind.Series))
                        yield return new AddGroup(group);
                foreach (var meld in state.Table.OrderBy(m => m.Id))
                    foreach (var id in player.Hand.OrderBy(id => id.Value))
                    {
                        token.ThrowIfCancellationRequested(); var joker = TileCatalog.IsRealJoker(id, indicator);
                        if (meld.Group.Kind != MeldKind.Pair && (player.Opening != OpeningKind.Pairs || state.SeriesTilesUsed < 2))
                            foreach (var face in ExtensionFaces(meld.Group))
                                if (joker || TileCatalog.GetNonJokerFace(id, indicator) == face)
                                    yield return new ExtendMeld(meld.Id, meld.Group.Kind == MeldKind.Run && face.Number < meld.Group.Tiles.Min(t => t.RepresentedFace.Number), new[] { new PlacedTile(id, face) });
                        if (joker || (meld.Group.Kind != MeldKind.Pair && player.Opening == OpeningKind.Pairs && state.SeriesTilesUsed >= 2)) continue;
                        foreach (var placed in meld.Group.Tiles)
                            if (TileCatalog.IsRealJoker(placed.Id, indicator) && TileCatalog.GetNonJokerFace(id, indicator) == placed.RepresentedFace)
                                yield return new SwapJoker(meld.Id, placed.Id, id);
                    }
            }
            private static IEnumerable<TileFace> ExtensionFaces(MeldProposal group)
            {
                if (group.Kind == MeldKind.Run)
                {
                    var minimum = group.Tiles.Min(t => t.RepresentedFace.Number); var maximum = group.Tiles.Max(t => t.RepresentedFace.Number);
                    if (minimum > 1) yield return new TileFace(group.Tiles[0].RepresentedFace.Color, minimum - 1);
                    if (maximum < 13) yield return new TileFace(group.Tiles[0].RepresentedFace.Color, maximum + 1);
                }
                else if (group.Kind == MeldKind.Set)
                    for (var color = 0; color < 4; color++)
                        if (!group.Tiles.Any(t => (int)t.RepresentedFace.Color == color)) yield return new TileFace((TileColor)color, group.Tiles[0].RepresentedFace.Number);
            }
            private void Consider(RoundState state, IReadOnlyList<TableAction> path)
            { foreach (var discard in state.Players[state.ActivePlayer].Hand.OrderBy(id => id.Value)) Consider(state, path, discard); }
            private void Consider(RoundState state, IReadOnlyList<TableAction> path, TileId discard)
            {
                token.ThrowIfCancellationRequested(); var player = state.Players[state.ActivePlayer];
                if (!player.Hand.Contains(discard)) return;
                var winning = player.Hand.Count == 1 && player.Opening != OpeningKind.None;
                var penalty = PenaltyEvaluator.GetDiscardPenalty(state, discard, winning);
                var remaining = player.Hand.Where(id => id != discard).ToArray();
                var jokers = remaining.Count(id => TileCatalog.IsRealJoker(id, indicator));
                var sum = remaining.Where(id => !TileCatalog.IsRealJoker(id, indicator)).Sum(id => TileCatalog.GetNonJokerFace(id, indicator).Number);
                // Rank unopened racks by their prospective opened penalty rather than the
                // constant 202, so a safe fallback discards high-value tiles consistently.
                var value = ScoreCalculator.Calculate(player.Opening == OpeningKind.None ? OpeningKind.Series : player.Opening,
                    false, sum, jokers, 0, HeuristicFinish).B;
                var plan = new TurnPlan(path, discard, penalty != 0);
                var rank = new Rank(winning, value, remaining.Length, penalty, PlanKey(plan));
                if (best != null && rank.Compare(best.Rank) >= 0) return;
                if (!TurnEvaluator.Evaluate(root, plan).Success) return;
                best = new RankedPlan { Plan = plan, Rank = rank };
            }
            private static string GroupKey(MeldProposal group)
                => ((int)group.Kind).ToString() + ":" + string.Join(",", group.Tiles.OrderBy(t => t.Id.Value).Select(t => $"{t.Id.Value:D3}/{(int)t.RepresentedFace.Color}/{t.RepresentedFace.Number:D2}"));
            private static string PlanKey(TurnPlan plan)
                => string.Join("|", plan.Actions.Select(a => a is OpenGroups open ? "0" + (int)open.Opening + string.Join(";", open.Groups.Select(GroupKey))
                    : a is AddGroup add ? "1" + GroupKey(add.Group)
                    : a is ExtendMeld extend ? $"2{extend.MeldId:D10}/{extend.Prepend}/" + string.Join(",", extend.Tiles.Select(t => $"{t.Id.Value:D3}/{(int)t.RepresentedFace.Color}/{t.RepresentedFace.Number:D2}"))
                    : a is SwapJoker swap ? $"3{swap.MeldId:D10}/{swap.JokerId.Value:D3}/{swap.ReplacementId.Value:D3}" : throw new InvalidOperationException("Unknown bot action."))) + $"D{plan.Discard.Value:D3}";
            private static string StateKey(RoundState state)
            {
                var player = state.Players[state.ActivePlayer];
                var key = new StringBuilder().Append((int)player.Opening).Append('/').Append(state.SeriesTilesUsed).Append(':');
                foreach (var id in player.Hand.OrderBy(id => id.Value)) key.Append(id.Value).Append(',');
                foreach (var meld in state.Table.OrderBy(m => m.Id)) key.Append('|').Append(meld.Id).Append('/').Append(meld.Owner).Append('/').Append(GroupKey(meld.Group));
                return key.ToString();
            }
        }
    }
}
