using System;
using System.Linq;
using NUnit.Framework;
using Okey101.Core.Model;
using Okey101.Core.Random;
using Okey101.Core.Turns;

namespace Okey101.Core.Tests
{
    public sealed class DeckTests
    {
        private static readonly Guid RoundId = new Guid("7a4a3a9a-1a9a-482c-9873-eedb877a35c1");

        [Test]
        public void CatalogHas106UniqueTiles()
        {
            var tiles = TileCatalog.Create();
            Assert.That(tiles.Count, Is.EqualTo(106));
            Assert.That(tiles.Select(t => t.Id).Distinct().Count(), Is.EqualTo(106));
            Assert.That(tiles.Count(t => !t.PrintedFace.HasValue), Is.EqualTo(2));
            Assert.That(tiles.Where(t => t.PrintedFace.HasValue).GroupBy(t => t.PrintedFace.Value)
                .All(group => group.Count() == 2), Is.True);
            Assert.That(tiles[0].PrintedFace, Is.EqualTo(new TileFace(TileColor.Red, 1)));
            Assert.That(tiles[103].PrintedFace, Is.EqualTo(new TileFace(TileColor.Black, 13)));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void DealConservesAllTiles(int starter)
        {
            var round = RoundFactory.Create(1, RoundId, starter);
            Assert.That(round.Players.Select(p => p.Hand.Count),
                Is.EqualTo(Enumerable.Range(0, 4).Select(p => p == starter ? 22 : 21)));
            Assert.That(round.Stock.Count, Is.EqualTo(20));
            Assert.That(round.ActivePlayer, Is.EqualTo(starter));
            Assert.That(round.Phase, Is.EqualTo(RoundPhase.PlanningTurn));
            Assert.That(round.PendingDraw, Is.Null);
            Assert.That(round.IsInitialTurn, Is.True);
            Assert.That(StateInvariant.Validate(round).Success, Is.True);
            var ids = round.Players.SelectMany(p => p.Hand).Concat(round.Stock).Append(round.Indicator).ToArray();
            Assert.That(ids.Length, Is.EqualTo(106));
            Assert.That(ids.Distinct().Count(), Is.EqualTo(106));
        }

        [Test]
        public void Indicator13WrapsJokerTo1()
        {
            var indicator = new TileFace(TileColor.Red, 13);
            Assert.That(TileCatalog.IsRealJoker(new TileId(0), indicator), Is.True);
            Assert.That(TileCatalog.IsRealJoker(new TileId(1), indicator), Is.True);
            Assert.That(TileCatalog.IsRealJoker(new TileId(26), indicator), Is.False);
            Assert.That(TileCatalog.GetNonJokerFace(new TileId(104), indicator), Is.EqualTo(new TileFace(TileColor.Red, 1)));
            Assert.That(TileCatalog.GetNonJokerFace(new TileId(105), indicator), Is.EqualTo(new TileFace(TileColor.Red, 1)));
            Assert.Throws<InvalidOperationException>(() => TileCatalog.GetNonJokerFace(new TileId(0), indicator));
        }

        [Test]
        public void RandomHasStableGoldenVector()
        {
            var random = new SeededRandom(1);
            Assert.That(new[] { random.NextUInt(), random.NextUInt(), random.NextUInt() },
                Is.EqualTo(new uint[] { 270369, 67634689, 2647435461 }));
        }

        [Test]
        public void RandomRejectsIncompleteUpperBucket()
        {
            // This seed's first xorshift output is uint.MaxValue; the next is 253983.
            Assert.That(new SeededRandom(1584200935).NextUInt(), Is.EqualTo(uint.MaxValue));
            // The generator has 2^32 - 1 possible nonzero outputs, not 2^32 outputs.
            // For a two-element range, the incomplete upper bucket must be rejected.
            Assert.That(new SeededRandom(1584200935).NextInt(2), Is.EqualTo(0));
        }

        [Test]
        public void SameSeedAndStarterRepeatDeal()
        {
            var a = RoundFactory.Create(42, RoundId, 2);
            var b = RoundFactory.Create(42, RoundId, 2);
            Assert.That(a.Indicator, Is.EqualTo(b.Indicator));
            Assert.That(a.Stock, Is.EqualTo(b.Stock));
            for (var i = 0; i < 4; i++) Assert.That(a.Players[i].Hand, Is.EqualTo(b.Players[i].Hand));
            var random = new SeededRandom(0);
            Assert.That(random.NextUInt(), Is.Not.EqualTo(0u));
            Assert.That(random.NextInt(1), Is.EqualTo(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(-1));
        }

        [TestCase(-1)]
        [TestCase(106)]
        public void InvalidIdentityIsRejected(int value)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TileId(value));
        }

        [TestCase(0)]
        [TestCase(14)]
        public void InvalidNumberIsRejected(int number)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TileFace(TileColor.Red, number));
        }

        [TestCase(-1)]
        [TestCase(4)]
        public void InvalidStarterIsRejected(int starter)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => RoundFactory.Create(1, RoundId, starter));
        }

        [Test]
        public void RandomStarterStillProducesValidRound()
        {
            var state = RoundFactory.Create(0, RoundId, null);
            Assert.That(state.StartingPlayer, Is.InRange(0, 3));
            Assert.That(StateInvariant.Validate(state).Success, Is.True);
        }

        [Test]
        public void CoreHasNoUnityEngineReference()
        {
            Assert.That(typeof(RoundState).Assembly.GetReferencedAssemblies()
                .Any(a => a.Name.StartsWith("UnityEngine", StringComparison.Ordinal)), Is.False);
        }
    }
}
