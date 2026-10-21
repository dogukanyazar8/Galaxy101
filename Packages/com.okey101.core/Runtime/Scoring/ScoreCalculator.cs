using System;
using Okey101.Core.Model;

namespace Okey101.Core.Scoring
{
    public static class ScoreCalculator
    {
        /// <summary>
        /// Calculates one player's result from terminal round data. The caller includes fake
        /// jokers at their normal face value in normalSum, excluding all real jokers.
        /// </summary>
        public static ScoreBreakdown Calculate(OpeningKind opening, bool isWinner, int normalSum,
            int realJokerCount, int penalties, FinishInfo finish)
        {
            if (finish == null) throw new ArgumentNullException(nameof(finish));
            Immutable.EnumValue(opening, nameof(opening));
            if (normalSum < 0) throw new ArgumentOutOfRangeException(nameof(normalSum));
            if (realJokerCount < 0 || realJokerCount > 2)
                throw new ArgumentOutOfRangeException(nameof(realJokerCount));
            if (penalties < 0) throw new ArgumentOutOfRangeException(nameof(penalties));
            if (isWinner && (finish.Reason != FinishReason.DiscardWin || opening != finish.WinnerOpening))
                throw new ArgumentException("Winner status and opening must agree with the finish.", nameof(isWinner));

            if (finish.Reason == FinishReason.AllPairs)
                return new ScoreBreakdown(false, normalSum, realJokerCount, penalties, 0, 1, null);

            checked
            {
                var multiplier = 1;
                if (finish.DiscardedRealJoker) multiplier *= 2;
                if (finish.WinnerOpening == OpeningKind.Pairs) multiplier *= 2;
                if (finish.WinnerOpenedThisTurn) multiplier *= 2;

                int baseScore;
                if (isWinner) baseScore = -101;
                else if (opening == OpeningKind.None) baseScore = 202;
                else
                {
                    baseScore = normalSum + 101 * realJokerCount;
                    if (opening == OpeningKind.Pairs) baseScore *= 2;
                }

                var total = (baseScore + penalties) * multiplier;
                return new ScoreBreakdown(true, normalSum, realJokerCount, penalties, baseScore, multiplier, total);
            }
        }
    }
}
