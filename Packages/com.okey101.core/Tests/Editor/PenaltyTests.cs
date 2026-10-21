using System;
using NUnit.Framework;
using Okey101.Core.Model;
using Okey101.Core.Rules;
using static Okey101.Core.Tests.TableTest;

namespace Okey101.Core.Tests
{
    public sealed class PenaltyTests
    {
        [Test]
        public void SameDiscardHasOnlyOnePenalty()
        {
            var joker = Id(TileColor.Black, 1);
            var state = Builder(new[] { joker, Spare }, OpeningKind.Series)
                .WithTable(new Meld(0, 0, Run(TileColor.Red, 1, 2, 3))).Build();
            Assert.That(PenaltyEvaluator.GetDiscardPenalty(state, joker, false), Is.EqualTo(101));
        }

        [TestCase(OpeningKind.None, 0, 0)]
        [TestCase(OpeningKind.Series, 0, 101)]
        [TestCase(OpeningKind.Series, 4, 101)]
        [TestCase(OpeningKind.Pairs, 0, 101)]
        [TestCase(OpeningKind.Pairs, 1, 101)]
        [TestCase(OpeningKind.Pairs, 2, 0)]
        public void BudgetAndOpeningAffectPenalty(OpeningKind opening, int used, int expected)
        {
            var state = TableTest.Context(Builder(new[] { Id(TileColor.Red, 4), Spare }, opening)
                .WithOpening(1, OpeningKind.Series).WithTable(new Meld(0, 1, Run(TileColor.Red, 1, 2, 3))).Build(), used);
            Assert.That(PenaltyEvaluator.GetDiscardPenalty(state, Id(TileColor.Red, 4), false), Is.EqualTo(expected));
        }

        [TestCase(OpeningKind.None)]
        [TestCase(OpeningKind.Pairs)]
        public void RealJokerPenaltyIsIndependentOfProcessingPermission(OpeningKind opening)
        {
            var joker = Id(TileColor.Black, 1);
            var state = TableTest.Context(Builder(new[] { joker, Spare }, opening).Build(), 2);
            Assert.That(PenaltyEvaluator.GetDiscardPenalty(state, joker, false), Is.EqualTo(101));
        }

        [Test]
        public void FinishAndExcludedActions()
        {
            var joker = Id(TileColor.Black, 1);
            Assert.That(PenaltyEvaluator.GetDiscardPenalty(Builder(new[] { joker }, OpeningKind.Series).Build(), joker, true), Is.Zero);
            var pairOnly = Builder(new[] { Id(TileColor.Red, 1), Id(TileColor.Red, 1, 1), Spare }, OpeningKind.Pairs).Build();
            Assert.That(PenaltyEvaluator.GetDiscardPenalty(pairOnly, Id(TileColor.Red, 1), false), Is.Zero);
            var swapOnly = Builder(new[] { Id(TileColor.Red, 2), Spare }, OpeningKind.Series)
                .WithTable(new Meld(0, 0, new MeldProposal(MeldKind.Run,
                    new[] { Normal(TileColor.Red, 1), Joker(0, TileColor.Red, 2), Normal(TileColor.Red, 3) }))).Build();
            Assert.That(PenaltyEvaluator.GetDiscardPenalty(swapOnly, Id(TileColor.Red, 2), false), Is.Zero);
        }

        [Test]
        public void FakeJokerUsesItsFixedFaceForDirectProcessing()
        {
            var fake = new TileId(104);
            var state = Builder(new[] { fake, Spare }, OpeningKind.Series)
                .WithTable(new Meld(0, 0, Run(TileColor.Black, 2, 3, 4))).Build();
            Assert.That(PenaltyEvaluator.GetDiscardPenalty(state, fake, false), Is.EqualTo(101));
        }

        [Test]
        public void DirectProcessingCannotRebindExistingJoker()
        {
            var tile = Id(TileColor.Red, 3);
            var state = Builder(new[] { tile, Spare }, OpeningKind.Series)
                .WithTable(new Meld(0, 0, new MeldProposal(MeldKind.Run,
                    new[] { Normal(TileColor.Red, 1), Normal(TileColor.Red, 2), Joker(0, TileColor.Red, 3) }))).Build();
            Assert.That(PenaltyEvaluator.GetDiscardPenalty(state, tile, false), Is.Zero);
        }

        [Test]
        public void MissingSetColorCanBeProcessed()
        {
            var tile = Id(TileColor.Black, 7);
            var state = Builder(new[] { tile, Spare }, OpeningKind.Series)
                .WithTable(new Meld(0, 0, new MeldProposal(MeldKind.Set,
                    new[] { Normal(TileColor.Red, 7), Normal(TileColor.Blue, 7), Normal(TileColor.Yellow, 7) }))).Build();
            Assert.That(PenaltyEvaluator.GetDiscardPenalty(state, tile, false), Is.EqualTo(101));
        }

        [Test]
        public void InvalidProgrammerArgumentsAreRejected()
        {
            Assert.Throws<ArgumentNullException>(() => PenaltyEvaluator.GetDiscardPenalty(null, Spare, false));
            Assert.Throws<ArgumentException>(() => PenaltyEvaluator.GetDiscardPenalty(
                Builder(new[] { Spare }).Build(), Id(TileColor.Red, 4), false));
        }
    }
}
