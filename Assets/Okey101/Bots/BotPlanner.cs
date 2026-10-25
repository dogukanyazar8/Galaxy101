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
            // Given back this turn, the pile tile cannot be taken again (engine: PreviousDiscardUnavailable).
            if (view.Discards[(view.Actor + 3) % 4].Count == 0 || view.PreviousDiscardReturned) return new DrawDecision(DrawSource.Stock);
            var witness = DiscardTurnFinder.Find(view, token, true);
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
                var found = DiscardTurnFinder.Find(beforeDraw, token, true);
                // A draw made by a player (a timed-out human) may need a copy mix only the full pool holds.
                if (found.Error == ErrorCode.NoLegalUse) found = DiscardTurnFinder.Find(beforeDraw, token, false);
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
                view.Discards, null, view.SeriesTilesUsed, null, view.Deal, view.Options);
        private static PlayerView ChangeDraw(PlayerView view, bool add, TileId tile)
        {
            var hand = view.OwnHand.ToList(); var piles = view.Discards.ToArray(); var previous = (view.Actor + 3) % 4;
            if (add) { hand.Add(tile); piles[previous] = piles[previous].Take(piles[previous].Count - 1).ToArray(); }
            else { if (!hand.Remove(tile)) throw new ArgumentException("Drawn tile is absent."); piles[previous] = piles[previous].Concat(new[] { tile }).ToArray(); }
            var players = view.Players.ToArray(); players[view.Actor] = new PublicPlayerInfo(hand.Count, view.OwnOpening, view.OwnPenalties);
            return new PlayerView(view.RoundId, view.Version, view.Actor, view.ActivePlayer, add ? RoundPhase.PlanningTurn : RoundPhase.AwaitingDraw,
                hand, view.OwnOpening, view.OwnPenalties, view.OwnLastSequence, view.Indicator, view.StockCount, players, view.Table, piles,
                add ? new DrawReceipt(DrawSource.PreviousDiscard, tile, false) : null, view.SeriesTilesUsed, null, view.Deal, view.Options);
        }
        private sealed class RankedPlan
        {
            public TurnPlan Plan;
            public Rank Rank;
            private string key;
            /// <summary>The canonical tie-break between equally ranked plans, built only when a tie occurs.</summary>
            public string Key => key ?? (key = Search.PlanKey(Plan));
        }
        private readonly struct Rank
        {
            public readonly bool Winning;
            private readonly int value, count, penalty;
            public Rank(bool winning, int value, int count, int penalty)
            { Winning = winning; this.value = value; this.count = count; this.penalty = penalty; }
            public int CompareValue(Rank other)
            {
                var compared = other.Winning.CompareTo(Winning); if (compared != 0) return compared;
                compared = value.CompareTo(other.value); if (compared != 0) return compared;
                compared = count.CompareTo(other.count); return compared != 0 ? compared : penalty.CompareTo(other.penalty);
            }
        }
        private sealed class Search
        {
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
            /// <summary>The tile taken from the pile this turn: it must be laid, so patterns bind it first among its copies.</summary>
            private static TileId? Picked(RoundState state) => state.PendingDraw?.Source == DrawSource.PreviousDiscard ? state.PendingDraw.Tile : (TileId?)null;
            private bool Visit(int ceiling)
            { token.ThrowIfCancellationRequested(); if (nodes >= ceiling) return false; nodes++; return true; }
            /// <summary>A pool group: the pattern, what each of its tiles needs from a hand (fixed per search), its face total.</summary>
            private sealed class Pattern { public MeldProposal Group; public int[] Needs; public int Total; }
            private IReadOnlyList<Pattern> Groups(IReadOnlyList<TileId> hand)
            {
                var result = new List<MeldProposal>();
                foreach (var kind in new[] { MeldKind.Run, MeldKind.Set, MeldKind.Pair })
                    result.AddRange(MeldCandidateGenerator.GenerateDistinct(hand, indicator, kind, token));
                return result.OrderByDescending(g => g.Tiles.Count).ThenByDescending(g => g.Tiles.Sum(t => t.RepresentedFace.Number))
                    .ThenBy(GroupKey, StringComparer.Ordinal)
                    .Select(g => new Pattern { Group = g, Needs = MeldBinder.Needs(g, indicator), Total = g.Tiles.Sum(t => t.RepresentedFace.Number) }).ToArray();
            }
            private void Open(OpeningKind opening, IReadOnlyList<Pattern> pool, int ceiling)
            {
                var hand = root.Players[root.ActivePlayer].Hand;
                // 101 / 5 pairs, raised at a katlamalı table by the openings before this one.
                var bar = TableEvaluator.OpeningBar(root, root.ActivePlayer, opening);
                // Pool groups are patterns: each binds to the lowest free copies when picked (the pile tile first), so a
                // packing that needs any copy mix is reachable. Positions are the binder's.
                var binder = new MeldBinder(hand, indicator, Picked(root));
                var full = hand.Count == 32 ? uint.MaxValue : (1u << hand.Count) - 1;
                var groups = pool.Where(g => (g.Group.Kind == MeldKind.Pair) == (opening == OpeningKind.Pairs)).ToArray();
                var selected = new List<MeldProposal>();
                // spec/22 elden bitiş: every tile but one in series groups opens below 101. Pairs need no search of their own:
                // fifteen or more tiles but one hold at least seven pairs.
                if (opening == OpeningKind.Series && TableEvaluator.FinishesFromHand(hand.Count, hand.Count - 1))
                {
                    var coverCeiling = Math.Min(ceiling, nodes + ceiling / 4);
                    var byKey = new List<Pattern>[53];
                    foreach (var g in groups)
                        foreach (var key in g.Needs.Distinct())
                            (byKey[key] ?? (byKey[key] = new List<Pattern>())).Add(g);
                    // Lowest uncovered tile first: it is either the one tile left to discard or the lowest of a group.
                    void Cover(int pivot, uint covered, bool spared)
                    {
                        if (!Visit(coverCeiling)) return;
                        while (pivot < hand.Count && (covered & (1u << pivot)) != 0) pivot++;
                        if (pivot == hand.Count)
                        {
                            if (!spared) return;
                            var action = new OpenGroups(opening, selected);
                            var applied = TableEvaluator.Evaluate(root, new[] { action });
                            if (applied.Success) Explore(applied.Value, new List<TableAction> { action }, pool, ceiling);
                            return;
                        }
                        if (!spared) Cover(pivot + 1, covered | (1u << pivot), true);
                        var options = byKey[binder.KeyAt(pivot)];
                        if (options == null) return;
                        // The pivot is the lowest free tile: a group needing its face binds that very tile.
                        foreach (var group in options)
                        {
                            if (nodes >= coverCeiling) return;
                            if (!binder.TryMask(group.Needs, covered, out var mask) || (mask & (1u << pivot)) == 0) continue;
                            selected.Add(binder.Bind(group.Group, group.Needs, covered)); Cover(pivot + 1, covered | mask, spared); selected.RemoveAt(selected.Count - 1);
                        }
                    }
                    Cover(0, 0, false);
                }
                // Optimistic face sum: if even both jokers at 13 cannot reach the bar, do not
                // spend the series branch's budget enumerating impossible subsets.
                if (opening == OpeningKind.Series && hand.Sum(id => TileCatalog.IsRealJoker(id, indicator) ? 13 : TileCatalog.GetNonJokerFace(id, indicator).Number) < bar) return;
                void Select(int start, uint used, int total)
                {
                    if (!Visit(ceiling)) return;
                    if (opening == OpeningKind.Pairs ? selected.Count >= bar : total >= bar)
                    {
                        var action = new OpenGroups(opening, selected);
                        var applied = TableEvaluator.Evaluate(root, new[] { action });
                        if (applied.Success && applied.Value.Players[root.ActivePlayer].Hand.Count > 0)
                            Explore(applied.Value, new List<TableAction> { action }, pool, ceiling);
                        return;
                    }
                    // A pattern may be picked again (both copies of a run): indices never decrease, so each multiset once.
                    for (var i = start; i < groups.Length && nodes < ceiling; i++)
                    {
                        token.ThrowIfCancellationRequested(); var candidate = groups[i];
                        if (!binder.TryMask(candidate.Needs, used, out var mask) || (used | mask) == full) continue;
                        selected.Add(binder.Bind(candidate.Group, candidate.Needs, used)); Select(i, used | mask, total + candidate.Total); selected.RemoveAt(selected.Count - 1);
                    }
                }
                Select(0, 0, 0);
            }
            private void Explore(RoundState state, List<TableAction> path, IReadOnlyList<Pattern> pool, int ceiling)
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
            private IEnumerable<TableAction> Actions(RoundState state, IReadOnlyList<Pattern> pool)
            {
                var player = state.Players[state.ActivePlayer]; var binder = new MeldBinder(player.Hand, indicator, Picked(state));
                foreach (var pattern in pool)
                    if (pattern.Group.Tiles.Count < player.Hand.Count && binder.TryMask(pattern.Needs, 0, out _) &&
                        (pattern.Group.Kind == MeldKind.Pair ? player.Opening == OpeningKind.Pairs || state.Players.Any(p => p.Opening == OpeningKind.Pairs) : player.Opening == OpeningKind.Series))
                        yield return new AddGroup(binder.Bind(pattern.Group, pattern.Needs, 0));
                foreach (var meld in state.Table.OrderBy(m => m.Id))
                    foreach (var id in player.Hand.OrderBy(id => id.Value))
                    {
                        token.ThrowIfCancellationRequested(); var joker = TileCatalog.IsRealJoker(id, indicator);
                        if (meld.Group.Kind != MeldKind.Pair && state.Options.Processing && (player.Opening != OpeningKind.Pairs || state.SeriesTilesUsed < 2))
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
            {
                var hand = state.Players[state.ActivePlayer].Hand; var (jokers, sum) = Count(hand);
                // Ascending ids without sorting a copy: a set of at most 106 bits.
                ulong low = 0, high = 0;
                for (var i = 0; i < hand.Count; i++) { var v = hand[i].Value; if (v < 64) low |= 1UL << v; else high |= 1UL << (v - 64); }
                for (var v = 0; v < 106; v++)
                    if (((v < 64 ? low >> v : high >> (v - 64)) & 1UL) != 0)
                        Consider(state, path, new TileId(v), jokers, sum);
            }
            private void Consider(RoundState state, IReadOnlyList<TableAction> path, TileId discard)
            { var (jokers, sum) = Count(state.Players[state.ActivePlayer].Hand); Consider(state, path, discard, jokers, sum); }
            private (int Jokers, int Sum) Count(IReadOnlyList<TileId> hand)
            {
                int jokers = 0, sum = 0;
                foreach (var id in hand) if (TileCatalog.IsRealJoker(id, indicator)) jokers++; else sum += TileCatalog.GetNonJokerFace(id, indicator).Number;
                return (jokers, sum);
            }
            /// <summary>
            /// Ranks one discard. Every explored state tries every discard, so the cheap parts of the rank decide first:
            /// most lose to the best plan before a penalty is evaluated, a plan built or a tie-break key written.
            /// </summary>
            private void Consider(RoundState state, IReadOnlyList<TableAction> path, TileId discard, int handJokers, int handSum)
            {
                token.ThrowIfCancellationRequested(); var player = state.Players[state.ActivePlayer];
                if (!player.Hand.Contains(discard)) return;
                var winning = player.Hand.Count == 1 && player.Opening != OpeningKind.None;
                var joker = TileCatalog.IsRealJoker(discard, indicator);
                int jokers = handJokers - (joker ? 1 : 0), sum = handSum - (joker ? 0 : TileCatalog.GetNonJokerFace(discard, indicator).Number);
                // ScoreCalculator's B for a non-winner, an unopened rack counted as opened by series rather than the constant
                // 202, so a safe fallback discards high-value tiles consistently.
                var value = (sum + 101 * jokers) * (player.Opening == OpeningKind.Pairs ? 2 : 1);
                var count = player.Hand.Count - 1;
                if (best != null && new Rank(winning, value, count, 0).CompareValue(best.Rank) > 0) return; // no penalty can save it
                var penalty = PenaltyEvaluator.GetDiscardPenalty(state, discard, winning);
                var rank = new Rank(winning, value, count, penalty);
                var compared = best == null ? -1 : rank.CompareValue(best.Rank);
                if (compared > 0) return;
                var plan = new TurnPlan(path, discard, penalty != 0);
                if (compared == 0 && string.CompareOrdinal(PlanKey(plan), best.Key) >= 0) return;
                if (!TurnEvaluator.Evaluate(root, plan).Success) return;
                best = new RankedPlan { Plan = plan, Rank = rank };
            }
            private static string GroupKey(MeldProposal group)
                => ((int)group.Kind).ToString() + ":" + string.Join(",", group.Tiles.OrderBy(t => t.Id.Value).Select(t => $"{t.Id.Value:D3}/{(int)t.RepresentedFace.Color}/{t.RepresentedFace.Number:D2}"));
            internal static string PlanKey(TurnPlan plan)
                => string.Join("|", plan.Actions.Select(a => a is OpenGroups open ? "0" + (int)open.Opening + string.Join(";", open.Groups.Select(GroupKey))
                    : a is AddGroup add ? "1" + GroupKey(add.Group)
                    : a is ExtendMeld extend ? $"2{extend.MeldId:D10}/{extend.Prepend}/" + string.Join(",", extend.Tiles.Select(t => $"{t.Id.Value:D3}/{(int)t.RepresentedFace.Color}/{t.RepresentedFace.Number:D2}"))
                    : a is SwapJoker swap ? $"3{swap.MeldId:D10}/{swap.JokerId.Value:D3}/{swap.ReplacementId.Value:D3}" : throw new InvalidOperationException("Unknown bot action."))) + $"D{plan.Discard.Value:D3}";
            private static string StateKey(RoundState state) => TableEvaluator.PositionKey(state);
        }
    }
}
