using System;
using NUnit.Framework;
using Okey101.Core.Model;
using Okey101.Core.Scoring;

namespace Okey101.Core.Tests
{
    public sealed class ScoreCalculatorTests
    {
        [TestCase(OpeningKind.Series, false, 113)]
        [TestCase(OpeningKind.Pairs, false, 226)]
        [TestCase(OpeningKind.Series, true, 226)]
        [TestCase(OpeningKind.Pairs, true, 452)]
        public void KnownExamples(OpeningKind opening, bool discardedRealJoker, int expected)
        {
            var score = ScoreCalculator.Calculate(opening, false, 12, 1, 0,
                Win(OpeningKind.Series, false, discardedRealJoker));
            Assert.That(score.HasScore, Is.True);
            Assert.That(score.Total, Is.EqualTo(expected));
            Assert.That(score.N, Is.EqualTo(12));
            Assert.That(score.J, Is.EqualTo(1));
            Assert.That(score.P, Is.Zero);
        }

        [TestCase(false, false, false, 1, -101, 113)]
        [TestCase(false, false, true, 2, -202, 226)]
        [TestCase(false, true, false, 2, -202, 226)]
        [TestCase(false, true, true, 4, -404, 452)]
        [TestCase(true, false, false, 2, -202, 226)]
        [TestCase(true, false, true, 4, -404, 452)]
        [TestCase(true, true, false, 4, -404, 452)]
        [TestCase(true, true, true, 8, -808, 904)]
        public void AllEightFinishCombinations(bool pairs, bool openedThisTurn, bool realJoker,
            int multiplier, int winnerTotal, int loserTotal)
        {
            var opening = pairs ? OpeningKind.Pairs : OpeningKind.Series;
            var finish = Win(opening, openedThisTurn, realJoker);
            var winner = ScoreCalculator.Calculate(opening, true, 0, 0, 0, finish);
            var loser = ScoreCalculator.Calculate(OpeningKind.Series, false, 12, 1, 0, finish);
            Assert.That(winner.B, Is.EqualTo(-101));
            Assert.That(winner.M, Is.EqualTo(multiplier));
            Assert.That(winner.Total, Is.EqualTo(winnerTotal));
            Assert.That(loser.B, Is.EqualTo(113));
            Assert.That(loser.M, Is.EqualTo(multiplier));
            Assert.That(loser.Total, Is.EqualTo(loserTotal));
        }

        [Test]
        public void PenaltyOrder()
        {
            var score = ScoreCalculator.Calculate(OpeningKind.Pairs, false, 12, 1, 101,
                Win(OpeningKind.Series, false, true));
            Assert.That(score.B, Is.EqualTo(226));
            Assert.That(score.P, Is.EqualTo(101));
            Assert.That(score.M, Is.EqualTo(2));
            // spec/17 §6: B × M + P (penalties are not multiplied): 226 × 2 + 101.
            Assert.That(score.Total, Is.EqualTo(553));
        }

        [TestCase(101, -707)]
        [TestCase(202, -606)]
        public void WinnerPenaltiesAreAddedUnmultiplied(int penalties, int total)
        {
            var score = ScoreCalculator.Calculate(OpeningKind.Pairs, true, 0, 0, penalties,
                Win(OpeningKind.Pairs, true, true));
            Assert.That(score.B, Is.EqualTo(-101));
            Assert.That(score.P, Is.EqualTo(penalties));
            Assert.That(score.M, Is.EqualTo(8));
            Assert.That(score.Total, Is.EqualTo(total));
        }

        [Test]
        public void UnopenedDoesNotAddHeldJoker()
        {
            var score = ScoreCalculator.Calculate(OpeningKind.None, false, int.MaxValue, 2, 101,
                Win(OpeningKind.Series, false, true));
            Assert.That(score.B, Is.EqualTo(202));
            Assert.That(score.Total, Is.EqualTo(505)); // 202 × 2 + 101
        }

        [TestCase(OpeningKind.Series, 0, 0, 0)]
        [TestCase(OpeningKind.Pairs, 0, 0, 0)]
        [TestCase(OpeningKind.Series, 0, 2, 202)]
        [TestCase(OpeningKind.Pairs, 0, 2, 404)]
        public void EmptyNormalSumAndTwoJokers(OpeningKind opening, int normalSum, int jokers, int total)
        {
            var score = ScoreCalculator.Calculate(opening, false, normalSum, jokers, 0, Win());
            Assert.That(score.Total, Is.EqualTo(total));
        }

        [TestCase(OpeningKind.None, 0, 0, 0, 202)]
        [TestCase(OpeningKind.None, 12, 2, 101, 303)]
        [TestCase(OpeningKind.Series, 12, 1, 101, 214)]
        [TestCase(OpeningKind.Pairs, 12, 1, 101, 327)]
        public void StockEndHasNoWinnerBonus(OpeningKind opening, int normalSum, int jokers,
            int penalties, int total)
        {
            var score = ScoreCalculator.Calculate(opening, false, normalSum, jokers, penalties,
                End(FinishReason.StockExhausted));
            Assert.That(score.HasScore, Is.True);
            Assert.That(score.M, Is.EqualTo(1));
            Assert.That(score.Total, Is.EqualTo(total));
        }

        [Test]
        public void CancellationDoesNotWriteZeroScore()
        {
            var score = ScoreCalculator.Calculate(OpeningKind.Pairs, false, int.MaxValue, 2, int.MaxValue,
                End(FinishReason.AllPairs));
            Assert.That(score.HasScore, Is.False);
            Assert.That(score.Total, Is.Null);
            Assert.That(score.B, Is.Zero);
            Assert.That(score.M, Is.EqualTo(1));
            Assert.That(score.N, Is.EqualTo(int.MaxValue));
            Assert.That(score.J, Is.EqualTo(2));
            Assert.That(score.P, Is.EqualTo(int.MaxValue));
        }

        [TestCase(-1, 0, 0, "normalSum")]
        [TestCase(0, -1, 0, "realJokerCount")]
        [TestCase(0, 3, 0, "realJokerCount")]
        [TestCase(0, 0, -1, "penalties")]
        public void InvalidAmountsAreRejectedEvenForCancellation(int normalSum, int jokers,
            int penalties, string parameter)
        {
            var error = Assert.Throws<ArgumentOutOfRangeException>(() => ScoreCalculator.Calculate(
                OpeningKind.Pairs, false, normalSum, jokers, penalties, End(FinishReason.AllPairs)));
            Assert.That(error.ParamName, Is.EqualTo(parameter));
        }

        [Test]
        public void InvalidOpeningIsRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ScoreCalculator.Calculate(
                (OpeningKind)99, false, 0, 0, 0, Win()));
        }

        [Test]
        public void MissingFinishIsRejected()
        {
            Assert.Throws<ArgumentNullException>(() => ScoreCalculator.Calculate(
                OpeningKind.Series, false, 0, 0, 0, null));
        }

        [TestCase(FinishReason.StockExhausted)]
        [TestCase(FinishReason.AllPairs)]
        public void NonWinningFinishCannotHaveWinner(FinishReason reason)
        {
            Assert.Throws<ArgumentException>(() => ScoreCalculator.Calculate(
                OpeningKind.Pairs, true, 0, 0, 0, End(reason)));
        }

        [TestCase(OpeningKind.None)]
        [TestCase(OpeningKind.Pairs)]
        public void WinnerOpeningMustMatchFinish(OpeningKind opening)
        {
            Assert.Throws<ArgumentException>(() => ScoreCalculator.Calculate(
                opening, true, 0, 0, 0, Win(OpeningKind.Series)));
        }

        [TestCase(FinishReason.StockExhausted, true, false)]
        [TestCase(FinishReason.StockExhausted, false, true)]
        [TestCase(FinishReason.AllPairs, true, false)]
        [TestCase(FinishReason.AllPairs, false, true)]
        public void NonWinningFinishRejectsSpecialFlags(FinishReason reason, bool openedThisTurn, bool realJoker)
        {
            Assert.Throws<ArgumentException>(() => new FinishInfo(
                reason, null, OpeningKind.None, openedThisTurn, realJoker));
        }

        [TestCase(OpeningKind.Series, int.MaxValue, 1, 0, false)]
        [TestCase(OpeningKind.Pairs, 1073741824, 0, 0, false)]
        [TestCase(OpeningKind.Series, int.MaxValue, 0, 1, false)]
        [TestCase(OpeningKind.Series, 1073741824, 0, 0, true)]
        public void OverflowIsRejectedInsteadOfWrapping(OpeningKind opening, int normalSum,
            int jokers, int penalties, bool realJokerFinish)
        {
            Assert.Throws<OverflowException>(() => ScoreCalculator.Calculate(opening, false,
                normalSum, jokers, penalties, Win(OpeningKind.Series, false, realJokerFinish)));
        }

        private static FinishInfo Win(OpeningKind opening = OpeningKind.Series,
            bool openedThisTurn = false, bool realJoker = false)
            => new FinishInfo(FinishReason.DiscardWin, 0, opening, openedThisTurn, realJoker);

        private static FinishInfo End(FinishReason reason)
            => new FinishInfo(reason, null, OpeningKind.None, false, false);
    }
}
