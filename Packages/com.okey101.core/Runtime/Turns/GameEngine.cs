using System;
using System.Collections.Generic;
using System.Threading;
using Okey101.Core.Model;
using Okey101.Core.Rules;
using Okey101.Core.Scoring;

namespace Okey101.Core.Turns
{
    public static class GameEngine
    {
        /// <summary>Returns a new authority snapshot; rejected commands never change the source.</summary>
        public static RuleResult<RoundState> Apply(RoundState state, GameCommand command, CancellationToken token)
        {
            if (state == null || command == null) return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);
            var meta = command.Meta;
            if (meta.RoundId != state.RoundId) return RuleResult<RoundState>.Fail(ErrorCode.WrongRound);
            if (meta.Actor != state.ActivePlayer) return RuleResult<RoundState>.Fail(ErrorCode.WrongPlayer);
            if (meta.Sequence <= state.LastAcceptedSequences[meta.Actor])
                return RuleResult<RoundState>.Fail(ErrorCode.DuplicateCommand);
            if (meta.ExpectedVersion != state.Version) return RuleResult<RoundState>.Fail(ErrorCode.StaleVersion);
            if (state.Phase == RoundPhase.Finished || state.Phase == RoundPhase.Cancelled ||
                (command is DrawCommand && state.Phase != RoundPhase.AwaitingDraw) ||
                ((command is CommitTurnCommand || command is ReturnDiscardCommand || command is ShortOpeningCommand) && state.Phase != RoundPhase.PlanningTurn))
                return RuleResult<RoundState>.Fail(ErrorCode.WrongPhase);
            if (token.IsCancellationRequested) return RuleResult<RoundState>.Fail(ErrorCode.Cancelled);
            if (state.Version == long.MaxValue || !StateInvariant.ValidateAuthority(state).Success)
                return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);

            RuleResult<RoundState> result;
            switch (command)
            {
                case DrawCommand draw: result = Draw(state, draw, token); break;
                case CommitTurnCommand commit: result = Commit(state, commit); break;
                case ReturnDiscardCommand back: result = ReturnDiscard(state, back); break;
                case ShortOpeningCommand shortOpening: result = ShortOpening(state, shortOpening); break;
                default: return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);
            }
            if (!result.Success) return result;
            if (!StateInvariant.ValidateAuthority(result.Value).Success)
                return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);
            // The final cancellation check is the command's local acceptance boundary.
            return token.IsCancellationRequested ? RuleResult<RoundState>.Fail(ErrorCode.Cancelled) : result;
        }

        private static RuleResult<RoundState> Draw(RoundState state, DrawCommand command, CancellationToken token)
        {
            // spec/17 §4: the previous discard can always be taken; if it is of no use it goes back (ReturnDiscard). Once put
            // back, only the stock remains this turn.
            if (command.Source == DrawSource.PreviousDiscard && state.PreviousDiscardReturned)
                return RuleResult<RoundState>.Fail(ErrorCode.PreviousDiscardUnavailable);
            if (token.IsCancellationRequested) return RuleResult<RoundState>.Fail(ErrorCode.Cancelled);

            var stock = new List<TileId>(state.Stock);
            var discards = CopyDiscards(state);
            TileId tile;
            if (command.Source == DrawSource.Stock)
            {
                if (stock.Count == 0) return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);
                tile = stock[0];
                stock.RemoveAt(0);
            }
            else
            {
                var previous = (state.ActivePlayer + 3) % 4;
                var pile = new List<TileId>(discards[previous]);
                if (pile.Count == 0) return RuleResult<RoundState>.Fail(ErrorCode.PreviousDiscardUnavailable);
                tile = pile[pile.Count - 1];
                pile.RemoveAt(pile.Count - 1);
                discards[previous] = pile;
            }
            var players = CopyPlayers(state);
            var player = players[state.ActivePlayer];
            var hand = new List<TileId>(player.Hand) { tile };
            players[state.ActivePlayer] = new PlayerState(hand, player.Opening, player.Penalties);
            return RuleResult<RoundState>.Ok(new RoundState(state.RoundId, state.Version + 1, state.Seed,
                state.StartingPlayer, state.ActivePlayer, state.TurnNumber, RoundPhase.PlanningTurn, state.Indicator,
                stock, players, state.Table, discards,
                new DrawReceipt(command.Source, tile, command.Source == DrawSource.Stock && stock.Count == 0),
                player.Opening, 0, false, AcceptedSequences(state, command.Meta), options: state.Options));
        }

        private static RuleResult<RoundState> ReturnDiscard(RoundState state, ReturnDiscardCommand command)
        {
            var draw = state.PendingDraw;
            if (draw == null || draw.Source != DrawSource.PreviousDiscard) return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);
            var actor = state.ActivePlayer;
            var players = CopyPlayers(state);
            var hand = new List<TileId>(players[actor].Hand);
            if (!hand.Remove(draw.Tile)) return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);
            players[actor] = new PlayerState(hand, players[actor].Opening, players[actor].Penalties);
            var discards = CopyDiscards(state);
            var previous = (actor + 3) % 4;
            discards[previous] = new List<TileId>(discards[previous]) { draw.Tile };
            return RuleResult<RoundState>.Ok(new RoundState(state.RoundId, state.Version + 1, state.Seed,
                state.StartingPlayer, actor, state.TurnNumber, RoundPhase.AwaitingDraw, state.Indicator, state.Stock,
                players, state.Table, discards, null, state.OpeningAtTurnStart, 0, false,
                AcceptedSequences(state, command.Meta), null, true, state.Options));
        }

        /// <summary>Eksik açılış: +101 to a player who has not opened; nothing else changes.</summary>
        private static RuleResult<RoundState> ShortOpening(RoundState state, ShortOpeningCommand command)
        {
            var actor = state.ActivePlayer;
            var player = state.Players[actor];
            if (player.Opening != OpeningKind.None) return RuleResult<RoundState>.Fail(ErrorCode.InvalidOpening);
            if (player.Penalties > int.MaxValue - Penalties.Standard) return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);
            var players = CopyPlayers(state);
            players[actor] = new PlayerState(player.Hand, player.Opening, player.Penalties + Penalties.Standard);
            return RuleResult<RoundState>.Ok(new RoundState(state.RoundId, state.Version + 1, state.Seed,
                state.StartingPlayer, actor, state.TurnNumber, state.Phase, state.Indicator, state.Stock,
                players, state.Table, state.Discards, state.PendingDraw, state.OpeningAtTurnStart, state.SeriesTilesUsed,
                state.IsInitialTurn, AcceptedSequences(state, command.Meta), null, state.PreviousDiscardReturned, state.Options));
        }

        private static RuleResult<RoundState> Commit(RoundState state, CommitTurnCommand command)
        {
            var evaluation = TurnEvaluator.Evaluate(state, command.Plan);
            if (!evaluation.Success) return evaluation;
            var candidate = evaluation.Value;
            var actor = candidate.ActivePlayer;
            var player = candidate.Players[actor];
            FinishInfo finish = null;
            if (player.Hand.Count == 0)
            {
                var indicator = TileCatalog.Create()[state.Indicator.Value].PrintedFace.Value;
                finish = new FinishInfo(FinishReason.DiscardWin, actor, player.Opening,
                    state.OpeningAtTurnStart == OpeningKind.None, TileCatalog.IsRealJoker(command.Plan.Discard, indicator));
            }
            else
            {
                var allPairs = true;
                foreach (var other in candidate.Players) allPairs &= other.Opening == OpeningKind.Pairs;
                if (allPairs) finish = new FinishInfo(FinishReason.AllPairs, null, OpeningKind.None, false, false);
                else if (state.PendingDraw != null && state.PendingDraw.WasLastStock)
                    finish = new FinishInfo(FinishReason.StockExhausted, null, OpeningKind.None, false, false);
            }

            RoundResult result = null;
            RoundPhase phase;
            var nextActor = actor;
            var turnNumber = state.TurnNumber;
            var openingAtStart = state.OpeningAtTurnStart;
            if (finish != null)
            {
                var score = CalculateResult(candidate, finish);
                if (!score.Success) return RuleResult<RoundState>.Fail(score.Error);
                result = score.Value;
                phase = finish.Reason == FinishReason.AllPairs ? RoundPhase.Cancelled : RoundPhase.Finished;
            }
            else
            {
                if (turnNumber == int.MaxValue) return RuleResult<RoundState>.Fail(ErrorCode.InvalidState);
                phase = RoundPhase.AwaitingDraw;
                nextActor = (actor + 1) % 4;
                turnNumber++;
                openingAtStart = candidate.Players[nextActor].Opening;
            }
            return RuleResult<RoundState>.Ok(new RoundState(candidate.RoundId, state.Version + 1, candidate.Seed,
                candidate.StartingPlayer, nextActor, turnNumber, phase, candidate.Indicator, candidate.Stock,
                candidate.Players, candidate.Table, candidate.Discards, null, openingAtStart, 0, false,
                AcceptedSequences(state, command.Meta), result, options: state.Options));
        }

        private static RuleResult<RoundResult> CalculateResult(RoundState state, FinishInfo finish)
        {
            var indicator = TileCatalog.Create()[state.Indicator.Value].PrintedFace.Value;
            var scores = new ScoreBreakdown[4];
            try
            {
                for (var p = 0; p < scores.Length; p++)
                {
                    var normalSum = 0;
                    var realJokers = 0;
                    foreach (var id in state.Players[p].Hand)
                        if (TileCatalog.IsRealJoker(id, indicator)) realJokers++;
                        else normalSum += TileCatalog.GetNonJokerFace(id, indicator).Number;
                    scores[p] = ScoreCalculator.Calculate(state.Players[p].Opening, finish.Winner == p,
                        normalSum, realJokers, state.Players[p].Penalties, finish);
                }
            }
            catch (OverflowException)
            {
                return RuleResult<RoundResult>.Fail(ErrorCode.InvalidState);
            }
            return RuleResult<RoundResult>.Ok(new RoundResult(finish, scores));
        }

        private static PlayerState[] CopyPlayers(RoundState state)
        {
            var players = new PlayerState[4];
            for (var p = 0; p < players.Length; p++) players[p] = state.Players[p];
            return players;
        }

        private static IReadOnlyList<TileId>[] CopyDiscards(RoundState state)
        {
            var discards = new IReadOnlyList<TileId>[4];
            for (var p = 0; p < discards.Length; p++) discards[p] = state.Discards[p];
            return discards;
        }

        private static long[] AcceptedSequences(RoundState state, CommandMeta meta)
        {
            var sequences = new long[4];
            for (var p = 0; p < sequences.Length; p++) sequences[p] = state.LastAcceptedSequences[p];
            sequences[meta.Actor] = meta.Sequence;
            return sequences;
        }
    }
}
