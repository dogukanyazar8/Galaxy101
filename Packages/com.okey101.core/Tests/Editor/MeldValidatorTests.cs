using System;
using System.Linq;
using NUnit.Framework;
using Okey101.Core.Model;
using Okey101.Core.Rules;

namespace Okey101.Core.Tests
{
    public sealed class MeldValidatorTests
    {
        private static readonly TileFace Indicator = new TileFace(TileColor.Blue, 13);

        [TestCase(1, 2, 3, true, 6)]
        [TestCase(11, 12, 13, true, 36)]
        [TestCase(12, 13, 1, false, 0)]
        [TestCase(13, 1, 2, false, 0)]
        [TestCase(1, 2, 4, false, 0)]
        public void RunBoundaries(int first, int second, int third, bool valid, int total)
        {
            var group = new MeldProposal(MeldKind.Run, new[]
            {
                Normal(TileColor.Red, first), Normal(TileColor.Red, second), Normal(TileColor.Red, third)
            });
            var result = MeldValidator.Validate(group, Indicator);
            Assert.That(result.Success, Is.EqualTo(valid));
            if (valid) Assert.That(result.Value, Is.EqualTo(total));
        }

        [Test]
        public void RunRequiresThreeToThirteenTilesOfOneColor()
        {
            Assert.That(MeldValidator.Validate(new MeldProposal(MeldKind.Run, new[]
                { Normal(TileColor.Red, 1), Normal(TileColor.Red, 2) }), Indicator).Success, Is.False);
            Assert.That(MeldValidator.Validate(new MeldProposal(MeldKind.Run, new[]
                { Normal(TileColor.Red, 1), Normal(TileColor.Red, 2), Normal(TileColor.Yellow, 3) }), Indicator).Success, Is.False);
            var all = Enumerable.Range(1, 13).Select(n => Normal(TileColor.Red, n)).ToArray();
            Assert.That(MeldValidator.Validate(new MeldProposal(MeldKind.Run, all), Indicator).Value, Is.EqualTo(91));
            Assert.That(MeldValidator.Validate(new MeldProposal(MeldKind.Run,
                all.Concat(new[] { Joker(0, TileColor.Red, 13) }).ToArray()), Indicator).Success, Is.False);
        }

        [Test]
        public void RunMembershipIsIndependentOfSelectionOrder()
        {
            var group = new MeldProposal(MeldKind.Run, new[]
                { Normal(TileColor.Red, 3), Normal(TileColor.Red, 1), Normal(TileColor.Red, 2) });
            var result = MeldValidator.Validate(group, Indicator);
            Assert.That(result.Success, Is.True);
            Assert.That(result.Value, Is.EqualTo(6));
            Assert.That(group.Tiles.Select(t => t.RepresentedFace.Number), Is.EqualTo(new[] { 3, 1, 2 }));
        }

        [Test]
        public void RunCannotReplaceMissingNumberWithDuplicate()
        {
            var group = new MeldProposal(MeldKind.Run, new[]
                { Normal(TileColor.Red, 1), Normal(TileColor.Red, 1, 1), Normal(TileColor.Red, 3) });
            Assert.That(MeldValidator.Validate(group, Indicator).Error, Is.EqualTo(ErrorCode.InvalidGroup));
        }

        [Test]
        public void SetRequiresDistinctColors()
        {
            var three = new[] { Normal(TileColor.Red, 7), Normal(TileColor.Blue, 7), Normal(TileColor.Black, 7) };
            Assert.That(MeldValidator.Validate(new MeldProposal(MeldKind.Set, three), Indicator).Value, Is.EqualTo(21));
            var four = three.Concat(new[] { Normal(TileColor.Yellow, 7) }).ToArray();
            Assert.That(MeldValidator.Validate(new MeldProposal(MeldKind.Set, four), Indicator).Value, Is.EqualTo(28));
            var repeatedColor = new[] { Normal(TileColor.Red, 7), Normal(TileColor.Red, 7, 1), Normal(TileColor.Blue, 7) };
            Assert.That(MeldValidator.Validate(new MeldProposal(MeldKind.Set, repeatedColor), Indicator).Success, Is.False);
            Assert.That(MeldValidator.Validate(new MeldProposal(MeldKind.Set,
                four.Concat(new[] { Joker(0, TileColor.Red, 7) }).ToArray()), Indicator).Success, Is.False);
            Assert.That(MeldValidator.Validate(new MeldProposal(MeldKind.Set,
                new[] { Normal(TileColor.Red, 7), Normal(TileColor.Yellow, 8), Normal(TileColor.Black, 7) }), Indicator).Success, Is.False);
        }

        [Test]
        public void SetRequiresAtLeastThreeTiles()
        {
            Assert.That(MeldValidator.Validate(new MeldProposal(MeldKind.Set,
                new[] { Normal(TileColor.Red, 7), Normal(TileColor.Yellow, 7) }), Indicator).Success, Is.False);
        }

        [Test]
        public void PairAndTwoJokers()
        {
            var normal = new MeldProposal(MeldKind.Pair,
                new[] { Normal(TileColor.Red, 7), Normal(TileColor.Red, 7, 1) });
            Assert.That(MeldValidator.Validate(normal, Indicator).Value, Is.EqualTo(14));
            var jokers = new MeldProposal(MeldKind.Pair,
                new[] { Joker(0, TileColor.Red, 13), Joker(1, TileColor.Red, 13) });
            Assert.That(MeldValidator.Validate(jokers, Indicator).Value, Is.EqualTo(26));
        }

        [Test]
        public void PairRequiresExactlyTwoMatchingFaces()
        {
            Assert.That(MeldValidator.Validate(new MeldProposal(MeldKind.Pair,
                new[] { Normal(TileColor.Red, 7), Normal(TileColor.Yellow, 7) }), Indicator).Success, Is.False);
            Assert.That(MeldValidator.Validate(new MeldProposal(MeldKind.Pair,
                new[] { Normal(TileColor.Red, 7), Normal(TileColor.Red, 8) }), Indicator).Success, Is.False);
            Assert.That(MeldValidator.Validate(new MeldProposal(MeldKind.Pair,
                new[] { Normal(TileColor.Red, 7) }), Indicator).Success, Is.False);
            Assert.That(MeldValidator.Validate(new MeldProposal(MeldKind.Pair,
                new[] { Normal(TileColor.Red, 7), Normal(TileColor.Red, 7, 1), Joker(0, TileColor.Red, 7) }), Indicator).Success, Is.False);
        }

        [Test]
        public void TwoJokersUseDeclaredFaces()
        {
            var run = new MeldProposal(MeldKind.Run,
                new[] { Normal(TileColor.Red, 5), Joker(0, TileColor.Red, 6), Joker(1, TileColor.Red, 7) });
            Assert.That(MeldValidator.Validate(run, Indicator).Value, Is.EqualTo(18));
            var set = new MeldProposal(MeldKind.Set,
                new[] { Normal(TileColor.Red, 7), Joker(0, TileColor.Blue, 7), Joker(1, TileColor.Black, 7) });
            Assert.That(MeldValidator.Validate(set, Indicator).Value, Is.EqualTo(21));
            var repeated = new MeldProposal(MeldKind.Set,
                new[] { Normal(TileColor.Red, 7), Joker(0, TileColor.Red, 7), Joker(1, TileColor.Black, 7) });
            Assert.That(MeldValidator.Validate(repeated, Indicator).Success, Is.False);
        }

        [Test]
        public void FakeJokerIsNotWildcard()
        {
            var fake = new PlacedTile(new TileId(104), new TileFace(TileColor.Blue, 1));
            Assert.That(MeldValidator.Validate(new MeldProposal(MeldKind.Run,
                new[] { fake, Normal(TileColor.Blue, 2), Normal(TileColor.Blue, 3) }), Indicator).Value, Is.EqualTo(6));
            var forged = new PlacedTile(new TileId(104), new TileFace(TileColor.Red, 1));
            Assert.That(MeldValidator.Validate(new MeldProposal(MeldKind.Run,
                new[] { forged, Normal(TileColor.Red, 2), Normal(TileColor.Red, 3) }), Indicator).Error, Is.EqualTo(ErrorCode.InvalidTile));
            Assert.That(MeldValidator.Validate(new MeldProposal(MeldKind.Pair,
                new[] { fake, new PlacedTile(new TileId(105), new TileFace(TileColor.Blue, 1)) }), Indicator).Value, Is.EqualTo(2));
        }

        [Test]
        public void DuplicatePhysicalTileAndForgedNormalFaceAreRejected()
        {
            var repeated = Normal(TileColor.Red, 7);
            Assert.That(MeldValidator.Validate(new MeldProposal(MeldKind.Pair,
                new[] { repeated, repeated }), Indicator).Error, Is.EqualTo(ErrorCode.InvalidTile));
            var forged = new PlacedTile(new TileId(0), new TileFace(TileColor.Red, 3));
            Assert.That(MeldValidator.Validate(new MeldProposal(MeldKind.Run,
                new[] { Normal(TileColor.Red, 1, 1), Normal(TileColor.Red, 2), forged }), Indicator).Error, Is.EqualTo(ErrorCode.InvalidTile));
        }

        [Test]
        public void EmptyNullAndDefaultInputsFailWithoutThrowing()
        {
            Assert.That(MeldValidator.Validate(null, Indicator).Error, Is.EqualTo(ErrorCode.InvalidGroup));
            Assert.That(MeldValidator.Validate(new MeldProposal(MeldKind.Run, Array.Empty<PlacedTile>()), Indicator).Success, Is.False);
            var malformed = new MeldProposal(MeldKind.Run,
                new[] { default(PlacedTile), Normal(TileColor.Red, 2), Normal(TileColor.Red, 3) });
            Assert.That(MeldValidator.Validate(malformed, Indicator).Error, Is.EqualTo(ErrorCode.InvalidTile));
            Assert.That(MeldValidator.Validate(malformed, default).Error, Is.EqualTo(ErrorCode.InvalidState));
        }

        internal static TileId Id(TileColor color, int number, int copy = 0)
            => new TileId(2 * (13 * (int)color + number - 1) + copy);
        internal static PlacedTile Normal(TileColor color, int number, int copy = 0)
            => new PlacedTile(Id(color, number, copy), new TileFace(color, number));
        private static PlacedTile Joker(int copy, TileColor color, int number)
            => new PlacedTile(new TileId(52 + copy), new TileFace(color, number));
    }
}
