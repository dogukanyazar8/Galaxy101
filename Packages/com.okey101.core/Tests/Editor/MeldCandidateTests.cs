using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Okey101.Core.Model;
using Okey101.Core.Rules;

namespace Okey101.Core.Tests
{
    public sealed class MeldCandidateTests
    {
        private static readonly TileFace Indicator = new TileFace(TileColor.Blue, 13);
        private static TileId Id(TileColor color, int number, int copy = 0)
            => MeldValidatorTests.Id(color, number, copy);
        private static int Score(MeldProposal group) => group.Tiles.Sum(t => t.RepresentedFace.Number);
        private static string Key(MeldProposal group) => string.Join("|", group.Tiles
            .OrderBy(t => t.Id.Value).Select(t => t.Id.Value + ":" + (int)t.RepresentedFace.Color + ":" + t.RepresentedFace.Number));

        [Test]
        public void AmbiguousRunDefaultsToHighestScore()
        {
            var groups = MeldCandidateGenerator.Generate(new[] { Id(TileColor.Red, 5), new TileId(52), new TileId(53) }, Indicator, MeldKind.Run);
            Assert.That(groups.Count, Is.EqualTo(6));
            Assert.That(Score(groups[0]), Is.EqualTo(18));
            Assert.That(groups[0].Tiles.Select(t => t.RepresentedFace.Number), Is.EqualTo(new[] { 5, 6, 7 }));
            Assert.That(groups.Select(Score).Distinct(), Is.EquivalentTo(new[] { 12, 15, 18 }));
            Assert.That(groups.Select(Key).Distinct().Count(), Is.EqualTo(6));
        }

        [TestCase(1)]
        [TestCase(13)]
        public void JokerRunsDoNotWrapAtEitherBoundary(int number)
        {
            var groups = MeldCandidateGenerator.Generate(new[] { Id(TileColor.Red, number), new TileId(52), new TileId(53) }, Indicator, MeldKind.Run);
            Assert.That(groups.Count, Is.EqualTo(2));
            Assert.That(groups.All(g => g.Tiles.Select(t => t.RepresentedFace.Number)
                .SequenceEqual(number == 1 ? new[] { 1, 2, 3 } : new[] { 11, 12, 13 })), Is.True);
        }

        [Test]
        public void IncludesAllRunSubsetsAndPhysicalCopies()
        {
            var groups = MeldCandidateGenerator.Generate(new[]
                { Id(TileColor.Red, 1), Id(TileColor.Red, 2), Id(TileColor.Red, 3), Id(TileColor.Red, 4) }, Indicator, MeldKind.Run);
            Assert.That(groups.Count, Is.EqualTo(3));
            Assert.That(groups.Select(Score), Is.EqualTo(new[] { 10, 9, 6 }));
            var copies = MeldCandidateGenerator.Generate(new[]
                { Id(TileColor.Red, 1, 1), Id(TileColor.Red, 1), Id(TileColor.Red, 2), Id(TileColor.Red, 3) }, Indicator, MeldKind.Run);
            Assert.That(copies.Count, Is.EqualTo(2));
            Assert.That(copies.Select(g => g.Tiles[0].Id.Value), Is.EqualTo(new[] { 0, 1 }));
        }

        [Test]
        public void FullColorRunIncludesAll66Intervals()
        {
            var hand = Enumerable.Range(1, 13).Select(n => Id(TileColor.Red, n)).ToArray();
            var groups = MeldCandidateGenerator.Generate(hand, Indicator, MeldKind.Run);
            Assert.That(groups.Count, Is.EqualTo(66));
            Assert.That(Score(groups[0]), Is.EqualTo(91));
            Assert.That(groups[0].Tiles.Count, Is.EqualTo(13));
        }

        [Test]
        public void Dense22TileHandEnumeratesAllPhysicalChoices()
        {
            var hand = Enumerable.Range(1, 10)
                .SelectMany(n => new[] { Id(TileColor.Red, n), Id(TileColor.Red, n, 1) })
                .Concat(new[] { new TileId(52), new TileId(53) }).ToArray();
            var timer = System.Diagnostics.Stopwatch.StartNew();
            var groups = MeldCandidateGenerator.Generate(hand, Indicator, MeldKind.Run);
            timer.Stop();
            // Independently counted over intervals: choose optional joker positions,
            // multiply by 2 for every remaining natural copy and joker permutation.
            Assert.That(groups.Count, Is.EqualTo(124724));
            Assert.That(Score(groups[0]), Is.EqualTo(78));
            TestContext.WriteLine("Dense hand: 124724 candidates; elapsed ms=" + timer.ElapsedMilliseconds);
        }

        [Test]
        public void CanUseJokerWhileMatchingNormalTileIsHeld()
        {
            var naturalThree = Id(TileColor.Red, 3);
            var joker = new TileId(52);
            var groups = MeldCandidateGenerator.Generate(new[]
                { Id(TileColor.Red, 1), Id(TileColor.Red, 2), naturalThree, joker }, Indicator, MeldKind.Run);
            Assert.That(groups.Count, Is.EqualTo(6));
            Assert.That(groups.Any(g => g.Tiles.Count == 3 && Score(g) == 6 &&
                g.Tiles.Any(t => t.Id == joker && t.RepresentedFace.Number == 3) &&
                g.Tiles.All(t => t.Id != naturalThree)), Is.True);
        }

        [Test]
        public void FourColorsProduceFourTriplesAndOneQuadruple()
        {
            var groups = MeldCandidateGenerator.Generate(new[]
                { Id(TileColor.Black, 7), Id(TileColor.Red, 7), Id(TileColor.Blue, 7), Id(TileColor.Yellow, 7) }, Indicator, MeldKind.Set);
            Assert.That(groups.Count, Is.EqualTo(5));
            Assert.That(Score(groups[0]), Is.EqualTo(28));
            Assert.That(groups.Count(g => g.Tiles.Count == 3), Is.EqualTo(4));
            Assert.That(groups.All(g => g.Tiles.Select(t => t.RepresentedFace.Color).Distinct().Count() == g.Tiles.Count), Is.True);
        }

        [Test]
        public void SetWithTwoJokersIncludesEveryColorAndIdentityAssignment()
        {
            var groups = MeldCandidateGenerator.Generate(new[] { Id(TileColor.Red, 7), new TileId(52), new TileId(53) }, Indicator, MeldKind.Set);
            Assert.That(groups.Count, Is.EqualTo(6));
            Assert.That(groups.All(g => Score(g) == 21), Is.True);
            Assert.That(groups.Select(Key).Distinct().Count(), Is.EqualTo(6));
        }

        [Test]
        public void PairsUseMatchingFacesAndDeduplicatePermutations()
        {
            var pair = MeldCandidateGenerator.Generate(new[] { Id(TileColor.Red, 7, 1), Id(TileColor.Red, 7) }, Indicator, MeldKind.Pair);
            Assert.That(pair.Count, Is.EqualTo(1));
            Assert.That(Score(pair[0]), Is.EqualTo(14));
            Assert.That(MeldCandidateGenerator.Generate(new[] { Id(TileColor.Red, 7), Id(TileColor.Yellow, 7) }, Indicator, MeldKind.Pair), Is.Empty);
        }

        [Test]
        public void TwoJokersExposeAll52PairBindings()
        {
            var groups = MeldCandidateGenerator.Generate(new[] { new TileId(53), new TileId(52) }, Indicator, MeldKind.Pair);
            Assert.That(groups.Count, Is.EqualTo(52));
            Assert.That(groups[0].Tiles.Select(t => t.RepresentedFace),
                Is.EqualTo(new[] { new TileFace(TileColor.Red, 13), new TileFace(TileColor.Red, 13) }));
            Assert.That(groups.Take(4).Select(g => g.Tiles[0].RepresentedFace.Color),
                Is.EqualTo(new[] { TileColor.Red, TileColor.Yellow, TileColor.Blue, TileColor.Black }));
            Assert.That(groups.Select(Key).Distinct().Count(), Is.EqualTo(52));
        }

        [Test]
        public void FakeJokersUseOnlyTheirAssignedFace()
        {
            Assert.That(MeldCandidateGenerator.Generate(new[]
                { new TileId(104), Id(TileColor.Red, 2), Id(TileColor.Red, 3) }, Indicator, MeldKind.Run), Is.Empty);
            var run = MeldCandidateGenerator.Generate(new[]
                { new TileId(104), Id(TileColor.Blue, 2), Id(TileColor.Blue, 3) }, Indicator, MeldKind.Run);
            Assert.That(run.Count, Is.EqualTo(1));
            Assert.That(Score(run[0]), Is.EqualTo(6));
            var pair = MeldCandidateGenerator.Generate(new[] { new TileId(104), new TileId(105) }, Indicator, MeldKind.Pair);
            Assert.That(pair.Count, Is.EqualTo(1));
            Assert.That(Score(pair[0]), Is.EqualTo(2));
        }

        [TestCase(MeldKind.Run)]
        [TestCase(MeldKind.Set)]
        [TestCase(MeldKind.Pair)]
        public void CandidatesPreserveIdentityAreValidAndIgnoreInputOrder(MeldKind kind)
        {
            var hand = new List<TileId>();
            for (var n = 1; n <= 5; n++) { hand.Add(Id(TileColor.Red, n)); hand.Add(Id(TileColor.Red, n, 1)); }
            foreach (TileColor color in Enum.GetValues(typeof(TileColor)))
                for (var copy = 0; copy < 2; copy++) hand.Add(Id(color, 7, copy));
            hand.AddRange(new[] { new TileId(104), new TileId(105), new TileId(52), new TileId(53) });
            var before = hand.ToArray();
            var groups = MeldCandidateGenerator.Generate(hand, Indicator, kind);
            Assert.That(groups.Count, Is.GreaterThan(0));
            foreach (var group in groups)
            {
                Assert.That(group.Kind, Is.EqualTo(kind));
                Assert.That(MeldValidator.Validate(group, Indicator).Success, Is.True);
                Assert.That(group.Tiles.All(t => hand.Contains(t.Id)), Is.True);
                Assert.That(group.Tiles.Select(t => t.Id).Distinct().Count(), Is.EqualTo(group.Tiles.Count));
            }
            Assert.That(groups.Select(Key).Distinct().Count(), Is.EqualTo(groups.Count));
            Assert.That(hand, Is.EqualTo(before));
            var reversed = MeldCandidateGenerator.Generate(hand.AsEnumerable().Reverse().ToArray(), Indicator, kind);
            Assert.That(reversed.Select(Key), Is.EqualTo(groups.Select(Key)));
            Assert.That(groups.Select(Score), Is.Ordered.Descending);
        }

        [Test]
        public void EmptyOrInsufficientHandHasNoCandidates()
        {
            Assert.That(MeldCandidateGenerator.Generate(Array.Empty<TileId>(), Indicator, MeldKind.Run), Is.Empty);
            Assert.That(MeldCandidateGenerator.Generate(new[] { Id(TileColor.Red, 7) }, Indicator, MeldKind.Pair), Is.Empty);
        }

        [Test]
        public void RejectsDuplicateOversizeAndInvalidInput()
        {
            Assert.Throws<ArgumentNullException>(() => MeldCandidateGenerator.Generate(null, Indicator, MeldKind.Run));
            Assert.Throws<ArgumentException>(() => MeldCandidateGenerator.Generate(new[] { new TileId(0), new TileId(0) }, Indicator, MeldKind.Pair));
            Assert.Throws<ArgumentException>(() => MeldCandidateGenerator.Generate(Enumerable.Range(0, 23).Select(i => new TileId(i)).ToArray(), Indicator, MeldKind.Run));
            Assert.Throws<ArgumentException>(() => MeldCandidateGenerator.Generate(Array.Empty<TileId>(), default, MeldKind.Run));
            Assert.Throws<ArgumentOutOfRangeException>(() => MeldCandidateGenerator.Generate(Array.Empty<TileId>(), Indicator, (MeldKind)9));
        }
    }
}
