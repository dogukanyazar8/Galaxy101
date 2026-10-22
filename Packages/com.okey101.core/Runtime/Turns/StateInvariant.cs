using System;
using System.Collections.Generic;
using Okey101.Core.Model;
using Okey101.Core.Rules;
using Okey101.Core.Scoring;

namespace Okey101.Core.Turns
{
    public static class StateInvariant
    {
        /// <summary>Validates engine snapshots; table/turn evaluator drafts use Validate instead.</summary>
        public static RuleResult<bool> ValidateAuthority(RoundState state)
        {
            if (!Validate(state).Success) return RuleResult<bool>.Fail(ErrorCode.InvalidState);
            var indicator = TileCatalog.Create()[state.Indicator.Value].PrintedFace.Value;
            var actor = state.ActivePlayer;
            var active = state.Players[actor];
            var terminal = state.Phase == RoundPhase.Finished || state.Phase == RoundPhase.Cancelled;
            if (terminal != (state.Result != null)) return RuleResult<bool>.Fail(ErrorCode.InvalidState);
            for (var p = 0; p < 4; p++)
            {
                var count = state.Players[p].Hand.Count;
                var winner = terminal && state.Result.FinishInfo.Winner == p;
                if (count > (p == actor && state.Phase == RoundPhase.PlanningTurn ? 22 : 21) ||
                    (winner ? count != 0 : count == 0)) return RuleResult<bool>.Fail(ErrorCode.InvalidState);
            }
            var hasPairs = false;
            foreach (var player in state.Players) hasPairs |= player.Opening == OpeningKind.Pairs;
            foreach (var meld in state.Table)
            {
                var opening = state.Players[meld.Owner].Opening;
                if (!MeldValidator.Validate(meld.Group, indicator).Success || opening == OpeningKind.None ||
                    (meld.Group.Kind != MeldKind.Pair && opening != OpeningKind.Series) ||
                    (meld.Group.Kind == MeldKind.Pair && !hasPairs))
                    return RuleResult<bool>.Fail(ErrorCode.InvalidState);
            }
            if (state.SeriesTilesUsed != 0) return RuleResult<bool>.Fail(ErrorCode.InvalidState);

            if (terminal)
            {
                if (state.IsInitialTurn || state.PendingDraw != null || !ValidTerminal(state, indicator))
                    return RuleResult<bool>.Fail(ErrorCode.InvalidState);
            }
            else if (state.IsInitialTurn)
            {
                if (state.Phase != RoundPhase.PlanningTurn || actor != state.StartingPlayer || state.TurnNumber != 0 ||
                    state.PendingDraw != null || state.Table.Count != 0 || state.Stock.Count != 20 ||
                    state.OpeningAtTurnStart != OpeningKind.None)
                    return RuleResult<bool>.Fail(ErrorCode.InvalidState);
                for (var p = 0; p < 4; p++)
                    if (state.Players[p].Hand.Count != (p == actor ? 22 : 21) || state.Players[p].Opening != OpeningKind.None ||
                        (p != actor && (state.Players[p].Penalties != 0 || state.LastAcceptedSequences[p] != 0)) || state.Discards[p].Count != 0)
                        return RuleResult<bool>.Fail(ErrorCode.InvalidState);
            }
            else
            {
                if (state.OpeningAtTurnStart != active.Opening) return RuleResult<bool>.Fail(ErrorCode.InvalidState);
                if (state.Phase == RoundPhase.AwaitingDraw)
                {
                    if (state.PendingDraw != null || state.Stock.Count == 0) return RuleResult<bool>.Fail(ErrorCode.InvalidState);
                }
                else if (state.Phase == RoundPhase.PlanningTurn)
                {
                    if (active.Hand.Count < 2 || state.PendingDraw == null)
                        return RuleResult<bool>.Fail(ErrorCode.InvalidState);
                    var held = false;
                    foreach (var id in active.Hand) held |= id == state.PendingDraw.Tile;
                    if (!held || (state.PendingDraw.Source == DrawSource.Stock
                        ? state.PendingDraw.WasLastStock != (state.Stock.Count == 0) : state.Stock.Count == 0))
                        return RuleResult<bool>.Fail(ErrorCode.InvalidState);
                }
            }
            return RuleResult<bool>.Ok(true);
        }

        private static bool ValidTerminal(RoundState state, TileFace indicator)
        {
            var finish = state.Result.FinishInfo;
            var actor = state.ActivePlayer;
            if (state.Discards[actor].Count == 0) return false;
            if ((state.Phase == RoundPhase.Cancelled) != (finish.Reason == FinishReason.AllPairs)) return false;
            if (finish.Reason == FinishReason.DiscardWin)
            {
                if (finish.Winner != actor || finish.WinnerOpening != state.Players[actor].Opening ||
                    finish.WinnerOpenedThisTurn != (state.OpeningAtTurnStart == OpeningKind.None) ||
                    finish.DiscardedRealJoker != TileCatalog.IsRealJoker(
                        state.Discards[actor][state.Discards[actor].Count - 1], indicator)) return false;
            }
            else if (finish.Reason == FinishReason.StockExhausted)
            {
                if (state.Stock.Count != 0) return false;
                var allPairs = true;
                foreach (var player in state.Players) allPairs &= player.Opening == OpeningKind.Pairs;
                if (allPairs) return false;
            }
            else
                foreach (var player in state.Players) if (player.Opening != OpeningKind.Pairs) return false;

            try
            {
                for (var p = 0; p < 4; p++)
                {
                    var normalSum = 0;
                    var jokers = 0;
                    var player = state.Players[p];
                    foreach (var id in player.Hand)
                        if (TileCatalog.IsRealJoker(id, indicator)) jokers++;
                        else normalSum += TileCatalog.GetNonJokerFace(id, indicator).Number;
                    var expected = ScoreCalculator.Calculate(player.Opening, finish.Winner == p,
                        normalSum, jokers, player.Penalties, finish);
                    var score = state.Result.Scores[p];
                    if (score.HasScore != expected.HasScore || score.N != expected.N || score.J != expected.J ||
                        score.P != expected.P || score.B != expected.B || score.M != expected.M || score.Total != expected.Total)
                        return false;
                }
            }
            catch (OverflowException) { return false; }
            return true;
        }

        public static RuleResult<bool> Validate(RoundState state)
        {
            if (state == null || !TileCatalog.Create()[state.Indicator.Value].PrintedFace.HasValue)
                return RuleResult<bool>.Fail(ErrorCode.InvalidState);
            var locations = new HashSet<TileId>();
            if (!locations.Add(state.Indicator)) return RuleResult<bool>.Fail(ErrorCode.InvalidState);
            foreach (var tile in state.Stock)
                if (!locations.Add(tile)) return RuleResult<bool>.Fail(ErrorCode.InvalidState);
            foreach (var player in state.Players)
                foreach (var tile in player.Hand)
                    if (!locations.Add(tile)) return RuleResult<bool>.Fail(ErrorCode.InvalidState);
            foreach (var pile in state.Discards)
                foreach (var tile in pile)
                    if (!locations.Add(tile)) return RuleResult<bool>.Fail(ErrorCode.InvalidState);
            var groupIds = new HashSet<int>();
            var indicator = TileCatalog.Create()[state.Indicator.Value].PrintedFace.Value;
            foreach (var meld in state.Table)
            {
                if (!groupIds.Add(meld.Id)) return RuleResult<bool>.Fail(ErrorCode.InvalidState);
                foreach (var tile in meld.Group.Tiles)
                {
                    if (!locations.Add(tile.Id) || !tile.RepresentedFace.IsValid)
                        return RuleResult<bool>.Fail(ErrorCode.InvalidState);
                    if (!TileCatalog.IsRealJoker(tile.Id, indicator) &&
                        TileCatalog.GetNonJokerFace(tile.Id, indicator) != tile.RepresentedFace)
                        return RuleResult<bool>.Fail(ErrorCode.InvalidState);
                }
            }
            return locations.Count == 106 ? RuleResult<bool>.Ok(true) : RuleResult<bool>.Fail(ErrorCode.InvalidState);
        }
    }
}
