using System;
using System.Collections.Generic;

namespace Okey101.Core.Model
{
    public enum ErrorCode
    {
        None, InvalidState, InvalidTile, InvalidGroup, InvalidOpening, OpeningRequired,
        OpeningTypeLocked, ProcessingLimit, JokerBindingLocked, InvalidReplacement,
        PreviousDiscardUnavailable, DiscardMustBeUsed, LastTileRequired, PenaltyConfirmationRequired,
        WrongRound, WrongPlayer, WrongPhase, StaleVersion, DuplicateCommand, NoLegalUse, Cancelled
    }

    public readonly struct RuleResult<T>
    {
        private readonly T value;
        public bool Success { get; }
        public ErrorCode Error => Success ? ErrorCode.None : (error == ErrorCode.None ? ErrorCode.InvalidState : error);
        private readonly ErrorCode error;
        public T Value => Success ? value : throw new InvalidOperationException("A failed result has no value.");
        private RuleResult(bool success, T result, ErrorCode errorCode) { Success = success; value = result; error = errorCode; }
        public static RuleResult<T> Ok(T result) => new RuleResult<T>(true, result, ErrorCode.None);
        public static RuleResult<T> Fail(ErrorCode error)
        {
            Immutable.EnumValue(error, nameof(error));
            if (error == ErrorCode.None) throw new ArgumentException("Failure requires an error code.", nameof(error));
            return new RuleResult<T>(false, default, error);
        }
    }

    public enum FinishReason { DiscardWin, StockExhausted, AllPairs }

    public sealed class FinishInfo
    {
        public FinishReason Reason { get; }
        public int? Winner { get; }
        public OpeningKind WinnerOpening { get; }
        public bool WinnerOpenedThisTurn { get; }
        public bool DiscardedRealJoker { get; }
        public FinishInfo(FinishReason reason, int? winner, OpeningKind winnerOpening,
            bool winnerOpenedThisTurn, bool discardedRealJoker)
        {
            Immutable.EnumValue(reason, nameof(reason));
            Immutable.EnumValue(winnerOpening, nameof(winnerOpening));
            if (winner.HasValue) Immutable.Player(winner.Value, nameof(winner));
            if (reason == FinishReason.DiscardWin)
            {
                if (!winner.HasValue || winnerOpening == OpeningKind.None)
                    throw new ArgumentException("A winning finish requires an opened winner.");
            }
            else if (winner.HasValue || winnerOpening != OpeningKind.None || winnerOpenedThisTurn || discardedRealJoker)
                throw new ArgumentException("A non-winning finish cannot have winner attributes.");
            Reason = reason;
            Winner = winner;
            WinnerOpening = winnerOpening;
            WinnerOpenedThisTurn = winnerOpenedThisTurn;
            DiscardedRealJoker = discardedRealJoker;
        }
    }

    public sealed class ScoreBreakdown
    {
        public bool HasScore { get; }
        public int N { get; }
        public int J { get; }
        public int P { get; }
        public int B { get; }
        public int M { get; }
        public int? Total { get; }
        public ScoreBreakdown(bool hasScore, int n, int j, int p, int b, int m, int? total)
        {
            if (n < 0 || j < 0 || j > 2 || p < 0) throw new ArgumentOutOfRangeException(nameof(n));
            if (m != 1 && m != 2 && m != 4 && m != 8) throw new ArgumentOutOfRangeException(nameof(m));
            if (hasScore != total.HasValue) throw new ArgumentException("Score presence and total must agree.");
            HasScore = hasScore; N = n; J = j; P = p; B = b; M = m; Total = total;
        }
    }

    public sealed class RoundResult
    {
        public FinishInfo FinishInfo { get; }
        public IReadOnlyList<ScoreBreakdown> Scores { get; }
        public RoundResult(FinishInfo finishInfo, IReadOnlyList<ScoreBreakdown> scores)
        {
            FinishInfo = finishInfo ?? throw new ArgumentNullException(nameof(finishInfo));
            Scores = Immutable.Copy(scores, nameof(scores));
            if (Scores.Count != 4) throw new ArgumentException("Exactly four score records are required.", nameof(scores));
            foreach (var score in Scores)
                if (score.HasScore == (finishInfo.Reason == FinishReason.AllPairs))
                    throw new ArgumentException("Cancelled and scored results cannot be mixed.", nameof(scores));
        }
    }

    public enum VisibleEventKind { Draw, GroupsOpened, GroupAdded, GroupExtended, JokerSwapped, Discard, RoundFinished, RoundCancelled }

    public sealed class VisibleEvent
    {
        public VisibleEventKind Kind { get; }
        public int Actor { get; }
        public TileId? TileId { get; }
        public int? MeldId { get; }
        public VisibleEvent(VisibleEventKind kind, int actor, TileId? tileId = null, int? meldId = null)
        {
            Immutable.EnumValue(kind, nameof(kind));
            Immutable.Player(actor, nameof(actor));
            if (meldId < 0) throw new ArgumentOutOfRangeException(nameof(meldId));
            Kind = kind; Actor = actor; TileId = tileId; MeldId = meldId;
        }
    }

    public sealed class CommandReceipt
    {
        public bool Accepted { get; }
        public ErrorCode Error { get; }
        public long Version { get; }
        public IReadOnlyList<VisibleEvent> Events { get; }
        public CommandReceipt(bool accepted, ErrorCode error, long version, IReadOnlyList<VisibleEvent> events)
        {
            if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
            Immutable.EnumValue(error, nameof(error));
            if (accepted != (error == ErrorCode.None)) throw new ArgumentException("Acceptance and error must agree.");
            Accepted = accepted; Error = error; Version = version; Events = Immutable.Copy(events, nameof(events));
        }
    }
}
