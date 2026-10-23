using System.Collections.Generic;
using Okey101.Core.Model;
using Okey101.Core.Turns;

namespace Okey101.Core.Rules
{
    public static class TableEvaluator
    {
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
                    case ExtendMeld extend: return Extend(extend);
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
                // spec/22 elden bitiş: a player who has not opened may lay everything but the last tile without reaching
                // 101 / 5 pairs and finish with that discard (the ×2 "opened this turn" finish).
                bool finishingFromHand = hand.Count >= 15 && hand.Count - selected.Count == 1;
                if (opening == OpeningKind.None && !finishingFromHand &&
                    (action.Opening == OpeningKind.Series ? total < 101 : action.Groups.Count < 5))
                    return ErrorCode.InvalidOpening;
                if (nextMeldId + action.Groups.Count - 1 > int.MaxValue) return ErrorCode.InvalidState;

                foreach (var id in selected) hand.Remove(id);
                foreach (var group in action.Groups)
                    table.Add(new Meld((int)nextMeldId++, source.ActivePlayer, group));
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
                        : new PlayerState(source.Players[player].Hand, source.Players[player].Opening, source.Players[player].Penalties + ownerPenalties[player]);
                players[source.ActivePlayer] = new PlayerState(hand, opening, players[source.ActivePlayer].Penalties);
                return new RoundState(source.RoundId, source.Version, source.Seed, source.StartingPlayer,
                    source.ActivePlayer, source.TurnNumber, source.Phase, source.Indicator, source.Stock,
                    players, table, source.Discards, source.PendingDraw, source.OpeningAtTurnStart,
                    seriesTilesUsed, source.IsInitialTurn, source.LastAcceptedSequences, source.Result);
            }
        }
    }
}
