using System.Collections.Generic;
using Okey101.Core.Model;
using Okey101.Core.Rules;

namespace Okey101.Core.Turns
{
    public static class TurnEvaluator
    {
        public static RuleResult<RoundState> Evaluate(RoundState drawnState, TurnPlan plan)
        {
            if (drawnState == null || plan == null) return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);
            var tableResult = TableEvaluator.Evaluate(drawnState, plan.Actions);
            if (!tableResult.Success) return tableResult;
            var candidate = tableResult.Value;
            var actor = candidate.ActivePlayer;
            var player = candidate.Players[actor];
            var hand = new List<TileId>(player.Hand);
            if (hand.Count == 0) return RuleResult<RoundState>.Fail(ErrorCode.LastTileRequired);
            if (!hand.Contains(plan.Discard)) return RuleResult<RoundState>.Fail(ErrorCode.InvalidTile);

            var draw = drawnState.PendingDraw;
            if (draw != null)
            {
                var originalHand = new List<TileId>(drawnState.Players[actor].Hand);
                if (!originalHand.Contains(draw.Tile)) return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);
                if (draw.Source == DrawSource.PreviousDiscard)
                {
                    if (plan.Discard == draw.Tile || hand.Contains(draw.Tile))
                        return RuleResult<RoundState>.Fail(ErrorCode.DiscardMustBeUsed);
                    if (drawnState.OpeningAtTurnStart == OpeningKind.None && !FirstOpeningContains(plan, draw.Tile))
                        return RuleResult<RoundState>.Fail(ErrorCode.DiscardMustBeUsed);
                }
            }

            var winning = hand.Count == 1;
            if (winning && player.Opening == OpeningKind.None)
                return RuleResult<RoundState>.Fail(ErrorCode.OpeningRequired);
            var penalty = PenaltyEvaluator.GetDiscardPenalty(candidate, plan.Discard, winning);
            if (penalty != 0 && !plan.AcceptPenalty)
                return RuleResult<RoundState>.Fail(ErrorCode.PenaltyConfirmationRequired);
            if (player.Penalties > int.MaxValue - penalty)
                return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);

            hand.Remove(plan.Discard);
            var players = new PlayerState[4];
            var discards = new IReadOnlyList<TileId>[4];
            for (var p = 0; p < players.Length; p++)
            {
                players[p] = candidate.Players[p];
                discards[p] = candidate.Discards[p];
            }
            players[actor] = new PlayerState(hand, player.Opening, player.Penalties + penalty);
            // spec/17 §3: opening with the previous player's discard costs that player the tile value × 10
            // (a discarded okey already cost +101 when it was thrown; it adds nothing here).
            if (draw != null && draw.Source == DrawSource.PreviousDiscard && drawnState.OpeningAtTurnStart == OpeningKind.None &&
                FirstOpeningContains(plan, draw.Tile))
            {
                var fed = Penalties.Fed(draw.Tile, TileCatalog.Create()[candidate.Indicator.Value].PrintedFace.Value);
                var giver = (actor + 3) % 4;
                if (fed > 0)
                {
                    if (players[giver].Penalties > int.MaxValue - fed) return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);
                    players[giver] = new PlayerState(players[giver].Hand, players[giver].Opening, players[giver].Penalties + fed);
                }
            }
            var pile = new List<TileId>(discards[actor]) { plan.Discard };
            discards[actor] = pile;
            return RuleResult<RoundState>.Ok(new RoundState(candidate.RoundId, candidate.Version, candidate.Seed,
                candidate.StartingPlayer, actor, candidate.TurnNumber, candidate.Phase, candidate.Indicator,
                candidate.Stock, players, candidate.Table, discards, candidate.PendingDraw, candidate.OpeningAtTurnStart,
                candidate.SeriesTilesUsed, candidate.IsInitialTurn, candidate.LastAcceptedSequences, candidate.Result));
        }

        private static bool FirstOpeningContains(TurnPlan plan, TileId tile)
        {
            foreach (var action in plan.Actions)
            {
                if (!(action is OpenGroups open)) continue;
                foreach (var group in open.Groups)
                    foreach (var placed in group.Tiles)
                        if (placed.Id == tile) return true;
                return false;
            }
            return false;
        }
    }
}
