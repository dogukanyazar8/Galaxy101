using System;
using System.Linq;
using Galaxy101.Table;
using NUnit.Framework;

namespace Galaxy101.EditorTests
{
    public sealed class Table2DLayoutTests
    {
        [TestCase(259, 539, 0, 0)]
        [TestCase(1015, 613, 1, 14)]
        [TestCase(225, 491, 0, 0)]
        public void RackDropMapsTheReferenceCells(float x, float y, int row, int column)
        {
            Assert.That(Table2DLayout.TrySlot(x, y, out var slot), Is.True);
            Assert.That(slot, Is.EqualTo(new RackSlot(row, column)));
        }

        [TestCase(10, 10)]
        [TestCase(1100, 620)]
        [TestCase(float.NaN, 520)]
        [TestCase(640, float.PositiveInfinity)]
        public void ADiscardOrInvalidPointerNeverBecomesARackMove(float x, float y)
        {
            Assert.That(Table2DLayout.TrySlot(x, y, out _), Is.False);
        }

        [Test]
        public void MeldsStayWithTheirOwnersAndPairsFollowRuns()
        {
            var poses = Table2DLayout.MeldTiles(new[]
            {
                new Table2DLayout.Meld(1, 0, new[] { 8, 9 }),
                new Table2DLayout.Meld(2, 0, new[] { 2, 4, 6 }),
                new Table2DLayout.Meld(3, 3, new[] { 10, 12, 14 }),
                new Table2DLayout.Meld(4, 1, new[] { 16, 18, 20 })
            });
            Assert.That(poses[2].X, Is.EqualTo(571));
            Assert.That(poses[2].Y, Is.EqualTo(356));
            Assert.That(poses[8].X, Is.EqualTo(659));
            Assert.That(poses[10].X, Is.EqualTo(165));
            Assert.That(poses[10].Y, Is.EqualTo(232));
            Assert.That(poses[16].X, Is.EqualTo(1039));
            Assert.That(poses.Keys.OrderBy(x => x), Is.EqualTo(new[] { 2, 4, 6, 8, 9, 10, 12, 14, 16, 18, 20 }));
        }

        [Test]
        public void DiscardAndLeftPickupHaveSeparateReferenceTargets()
        {
            Assert.That(Table2DLayout.Discard.Contains(1108, 603), Is.True);
            Assert.That(Table2DLayout.Side.Contains(176, 610), Is.True);
            Assert.That(Table2DLayout.Discard.Contains(176, 610), Is.False);
            Assert.That(Table2DLayout.Side.Contains(1108, 603), Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => Table2DLayout.RackTile(new RackSlot(2, 0)));
        }
    }
}
