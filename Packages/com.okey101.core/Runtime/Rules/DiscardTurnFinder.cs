using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Okey101.Core.Model;
using Okey101.Core.Turns;

namespace Okey101.Core.Rules
{
    public static class DiscardTurnFinder
    {
        /// <summary>Finds a legal witness using only the player's hand and public information.</summary>
        public static RuleResult<TurnPlan> Find(PlayerView beforeDraw, CancellationToken token)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                if (beforeDraw == null) return RuleResult<TurnPlan>.Fail(ErrorCode.InvalidState);
                if (beforeDraw.Actor != beforeDraw.ActivePlayer) return RuleResult<TurnPlan>.Fail(ErrorCode.WrongPlayer);
                if (beforeDraw.Phase != RoundPhase.AwaitingDraw) return RuleResult<TurnPlan>.Fail(ErrorCode.WrongPhase);
                var drawn = BuildHypotheticalDraw(beforeDraw);
                if (!drawn.Success) return RuleResult<TurnPlan>.Fail(drawn.Error);
                var search = new Search(drawn.Value, token);
                if (search.Run(out var plan)) return RuleResult<TurnPlan>.Ok(plan);
                token.ThrowIfCancellationRequested();
                return RuleResult<TurnPlan>.Fail(ErrorCode.NoLegalUse);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return RuleResult<TurnPlan>.Fail(ErrorCode.Cancelled);
            }
        }

        private static RuleResult<RoundState> BuildHypotheticalDraw(PlayerView view)
        {
            var actor = view.Actor;
            var previous = (actor + 3) % 4;
            var own = view.Players[actor];
            if (view.Result != null || view.PendingDraw != null || view.OwnHand.Count > 21 ||
                view.OwnHand.Count != own.HandCount || view.OwnOpening != own.Opening || view.OwnPenalties != own.Penalties ||
                (view.OwnOpening == OpeningKind.Pairs && view.SeriesTilesUsed > 2))
                return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);
            var indicator = TileCatalog.Create()[view.Indicator.Value].PrintedFace;
            if (!indicator.HasValue) return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);

            var occupied = new HashSet<TileId> { view.Indicator };
            foreach (var id in view.OwnHand)
                if (!occupied.Add(id)) return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);
            var meldIds = new HashSet<int>();
            foreach (var meld in view.Table)
            {
                if (!meldIds.Add(meld.Id) || !MeldValidator.Validate(meld.Group, indicator.Value).Success)
                    return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);
                foreach (var tile in meld.Group.Tiles)
                    if (!occupied.Add(tile.Id)) return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);
            }
            foreach (var pile in view.Discards)
                foreach (var id in pile)
                    if (!occupied.Add(id)) return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);
            if (view.Discards[previous].Count == 0)
                return RuleResult<RoundState>.Fail(ErrorCode.PreviousDiscardUnavailable);

            // Fill invisible locations canonically. Only their public counts are used; no
            // transition or goal below reads these fabricated faces or the stock order.
            var unknown = TileCatalog.Create().Select(t => t.Id).Where(id => !occupied.Contains(id)).ToArray();
            var needed = view.StockCount;
            for (var p = 0; p < 4; p++) if (p != actor) needed += view.Players[p].HandCount;
            if (needed != unknown.Length) return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);
            var picked = view.Discards[previous][view.Discards[previous].Count - 1];
            var players = new PlayerState[4];
            var discards = new IReadOnlyList<TileId>[4];
            var cursor = 0;
            for (var p = 0; p < 4; p++)
            {
                var hand = p == actor ? view.OwnHand.Concat(new[] { picked }).ToArray()
                    : unknown.Skip(cursor).Take(view.Players[p].HandCount).ToArray();
                if (p != actor) cursor += hand.Length;
                players[p] = new PlayerState(hand, view.Players[p].Opening, view.Players[p].Penalties);
                discards[p] = p == previous ? view.Discards[p].Take(view.Discards[p].Count - 1).ToArray() : view.Discards[p];
            }
            var sequences = new long[4];
            sequences[actor] = view.OwnLastSequence;
            return RuleResult<RoundState>.Ok(new RoundState(view.RoundId, view.Version, 0, actor, actor, 0,
                RoundPhase.PlanningTurn, view.Indicator, unknown.Skip(cursor).ToArray(), players, view.Table, discards,
                new DrawReceipt(DrawSource.PreviousDiscard, picked, false), view.OwnOpening, view.SeriesTilesUsed,
                false, sequences, options: view.Options));
        }

        private sealed class Search
        {
            private readonly RoundState root;
            private readonly TileId picked;
            private readonly TileFace indicator;
            private readonly CancellationToken token;
            private readonly HashSet<string> visited = new HashSet<string>();

            public Search(RoundState root, CancellationToken token)
            {
                this.root = root;
                this.token = token;
                picked = root.PendingDraw.Tile;
                indicator = TileCatalog.Create()[root.Indicator.Value].PrintedFace.Value;
            }

            public bool Run(out TurnPlan plan)
            {
                if (root.Players[root.ActivePlayer].Opening != OpeningKind.None)
                    return Explore(root, new List<TableAction>(), out plan);
                if (FindOpening(OpeningKind.Series, out plan)) return true;
                return FindOpening(OpeningKind.Pairs, out plan);
            }

            private bool TryFinish(RoundState state, List<TableAction> path, out TurnPlan plan)
            {
                plan = null;
                var hand = state.Players[state.ActivePlayer].Hand;
                if (hand.Contains(picked) || !state.Table.Any(m => m.Group.Tiles.Any(t => t.Id == picked))) return false;
                foreach (var discard in hand.OrderBy(id => TileCatalog.IsRealJoker(id, indicator) ? 1 : 0).ThenBy(id => id.Value))
                {
                    token.ThrowIfCancellationRequested();
                    var candidate = new TurnPlan(path, discard, true);
                    if (!TurnEvaluator.Evaluate(root, candidate).Success) continue;
                    plan = candidate;
                    return true;
                }
                return false;
            }

            private bool Explore(RoundState state, List<TableAction> path, out TurnPlan plan)
            {
                token.ThrowIfCancellationRequested();
                if (TryFinish(state, path, out plan)) return true;
                if (!visited.Add(Key(state))) return false;
                // Each action decreases 2 * normal tiles in hand + real jokers in hand.
                // Thus recursion depth is at most 44 for a 22-tile hand, including swaps.
                foreach (var action in Actions(state))
                {
                    token.ThrowIfCancellationRequested();
                    var next = TableEvaluator.Evaluate(state, new[] { action });
                    if (!next.Success || next.Value.Players[state.ActivePlayer].Hand.Count == 0) continue;
                    path.Add(action);
                    if (Explore(next.Value, path, out plan)) return true;
                    path.RemoveAt(path.Count - 1);
                }
                plan = null;
                return false;
            }

            private IEnumerable<TableAction> Actions(RoundState state)
            {
                foreach (var action in SimpleActions(state, true)) yield return action;
                var player = state.Players[state.ActivePlayer];
                var groups = new List<MeldProposal>();
                if (player.Opening == OpeningKind.Series)
                {
                    groups.AddRange(MeldCandidateGenerator.Generate(player.Hand, indicator, MeldKind.Run, token));
                    groups.AddRange(MeldCandidateGenerator.Generate(player.Hand, indicator, MeldKind.Set, token));
                }
                if (player.Opening == OpeningKind.Pairs || state.Players.Any(p => p.Opening == OpeningKind.Pairs))
                    groups.AddRange(MeldCandidateGenerator.Generate(player.Hand, indicator, MeldKind.Pair, token));
                foreach (var group in groups)
                {
                    token.ThrowIfCancellationRequested();
                    if (group.Tiles.Count < player.Hand.Count && group.Tiles.Any(t => t.Id == picked))
                        yield return new AddGroup(group);
                }
                foreach (var action in SimpleActions(state, false)) yield return action;
                foreach (var group in groups)
                {
                    token.ThrowIfCancellationRequested();
                    if (group.Tiles.Count < player.Hand.Count && !group.Tiles.Any(t => t.Id == picked))
                        yield return new AddGroup(group);
                }
            }

            private IEnumerable<TableAction> SimpleActions(RoundState state, bool pickedOnly)
            {
                var player = state.Players[state.ActivePlayer];
                foreach (var meld in state.Table.OrderBy(m => m.Id))
                {
                    if (meld.Group.Kind != MeldKind.Pair && player.Opening == OpeningKind.Pairs && state.SeriesTilesUsed >= 2)
                        continue;
                    foreach (var id in player.Hand.OrderBy(id => id.Value))
                    {
                        token.ThrowIfCancellationRequested();
                        if ((id == picked) != pickedOnly) continue;
                        var realJoker = TileCatalog.IsRealJoker(id, indicator);
                        foreach (var tile in meld.Group.Tiles)
                            if (!realJoker && TileCatalog.IsRealJoker(tile.Id, indicator) &&
                                TileCatalog.GetNonJokerFace(id, indicator) == tile.RepresentedFace)
                                yield return new SwapJoker(meld.Id, tile.Id, id);
                        if (meld.Group.Kind == MeldKind.Pair || player.Hand.Count <= 1) continue;
                        foreach (var face in ExtensionFaces(meld.Group))
                            if (realJoker || TileCatalog.GetNonJokerFace(id, indicator) == face)
                                yield return new ExtendMeld(meld.Id,
                                    meld.Group.Kind == MeldKind.Run && face.Number < meld.Group.Tiles.Min(t => t.RepresentedFace.Number),
                                    new[] { new PlacedTile(id, face) });
                    }
                }
            }

            private static IEnumerable<TileFace> ExtensionFaces(MeldProposal group)
            {
                if (group.Kind == MeldKind.Run)
                {
                    var minimum = group.Tiles.Min(t => t.RepresentedFace.Number);
                    var maximum = group.Tiles.Max(t => t.RepresentedFace.Number);
                    var color = group.Tiles[0].RepresentedFace.Color;
                    if (minimum > 1) yield return new TileFace(color, minimum - 1);
                    if (maximum < 13) yield return new TileFace(color, maximum + 1);
                }
                else if (group.Kind == MeldKind.Set)
                    for (var color = 0; color < 4; color++)
                        if (!group.Tiles.Any(t => (int)t.RepresentedFace.Color == color))
                            yield return new TileFace((TileColor)color, group.Tiles[0].RepresentedFace.Number);
            }

            private static string Key(RoundState state)
            {
                var player = state.Players[state.ActivePlayer];
                var key = new StringBuilder().Append((int)player.Opening).Append('/').Append(state.SeriesTilesUsed).Append('|');
                foreach (var id in player.Hand.OrderBy(id => id.Value)) key.Append(id.Value).Append(',');
                foreach (var meld in state.Table.OrderBy(m => m.Id))
                {
                    key.Append('|').Append(meld.Id).Append('/').Append(meld.Owner).Append('/').Append((int)meld.Group.Kind).Append(':');
                    foreach (var tile in meld.Group.Tiles.OrderBy(t => t.Id.Value))
                        key.Append(tile.Id.Value).Append('/').Append((int)tile.RepresentedFace.Color)
                            .Append('/').Append(tile.RepresentedFace.Number).Append(',');
                }
                return key.ToString();
            }

            private bool FindOpening(OpeningKind opening, out TurnPlan plan)
            {
                plan = null;
                var hand = root.Players[root.ActivePlayer].Hand.OrderBy(id => id.Value).ToArray();
                var pairs = opening == OpeningKind.Pairs;
                var threshold = pairs ? 5 : 101;
                var values = hand.Select(id => TileCatalog.IsRealJoker(id, indicator) ? 13 :
                    TileCatalog.GetNonJokerFace(id, indicator).Number).ToArray();
                if (pairs ? hand.Length < 11 : values.Sum() < 101) return false;

                var indexes = new Dictionary<TileId, int>();
                for (var i = 0; i < hand.Length; i++) indexes.Add(hand[i], i);
                var pickedBit = 1u << indexes[picked];
                var choices = new List<OpeningChoice>[hand.Length];
                for (var i = 0; i < choices.Length; i++) choices[i] = new List<OpeningChoice>();
                var kinds = pairs ? new[] { MeldKind.Pair } : new[] { MeldKind.Run, MeldKind.Set };
                foreach (var kind in kinds)
                    foreach (var group in MeldCandidateGenerator.Generate(hand, indicator, kind, token))
                    {
                        token.ThrowIfCancellationRequested();
                        uint mask = 0;
                        foreach (var tile in group.Tiles) mask |= 1u << indexes[tile.Id];
                        var choice = new OpeningChoice(group, mask, pairs ? 1 : group.Tiles.Sum(t => t.RepresentedFace.Number));
                        foreach (var tile in group.Tiles) choices[indexes[tile.Id]].Add(choice);
                    }

                var selected = new List<MeldProposal>();
                var failed = new HashSet<(uint Available, int Used, int Value, bool Picked, int Groups)>();
                // Under ordinary penalties every qualifying opening has an immediate discard.
                // At integer saturation, future processing/penalty legality can depend on the
                // actual selected bindings; do not merge those different opening histories.
                var memoize = root.Players[root.ActivePlayer].Penalties <= int.MaxValue - 101;
                TurnPlan witness = null;

                bool Select(uint available, int used, int value, bool containsPicked)
                {
                    token.ThrowIfCancellationRequested();
                    if (used >= hand.Length || (!containsPicked && (available & pickedBit) == 0)) return false;
                    // Group count also determines whether the remaining meld ID space is sufficient.
                    var key = (available, used, value, containsPicked, selected.Count);
                    if (memoize && failed.Contains(key)) return false;
                    if (containsPicked && value >= threshold)
                    {
                        var open = new OpenGroups(opening, selected);
                        var result = TableEvaluator.Evaluate(root, new TableAction[] { open });
                        if (result.Success && Explore(result.Value, new List<TableAction> { open }, out witness)) return true;
                    }

                    var remaining = 0;
                    var upper = 0;
                    var pivot = -1;
                    for (var i = 0; i < hand.Length; i++)
                    {
                        if ((available & (1u << i)) == 0) continue;
                        if (pivot < 0) pivot = i;
                        remaining++;
                        upper += values[i];
                    }
                    if (pivot < 0 || (pairs ? value + remaining / 2 < threshold : value + upper < threshold)) return false;
                    foreach (var choice in choices[pivot])
                    {
                        token.ThrowIfCancellationRequested();
                        if ((available & choice.Mask) != choice.Mask || used + choice.Group.Tiles.Count >= hand.Length) continue;
                        selected.Add(choice.Group);
                        if (Select(available & ~choice.Mask, used + choice.Group.Tiles.Count,
                            Math.Min(threshold, value + choice.Value), containsPicked || (choice.Mask & pickedBit) != 0)) return true;
                        selected.RemoveAt(selected.Count - 1);
                    }
                    if ((1u << pivot) != pickedBit && Select(available & ~(1u << pivot), used, value, containsPicked)) return true;
                    if (memoize) failed.Add(key);
                    return false;
                }

                var found = Select((1u << hand.Length) - 1, 0, 0, false);
                plan = witness;
                return found;
            }

            private readonly struct OpeningChoice
            {
                public MeldProposal Group { get; }
                public uint Mask { get; }
                public int Value { get; }
                public OpeningChoice(MeldProposal group, uint mask, int value)
                { Group = group; Mask = mask; Value = value; }
            }
        }
    }
}
