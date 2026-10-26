using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Galaxy101.Table;
using NUnit.Framework;

namespace Galaxy101.Tests
{
    /// <summary>"Seri diz" / "Çift diz" (RackArranger): the sets chosen and where they land on the 2 × 15 rack.</summary>
    public sealed class RackArrangerTests
    {
        // Colours in rack order: red, blue, yellow, black.
        private const int R = 0, B = 1, Y = 2, K = 3;
        private const int Okey = -1;

        /// <summary>Hands as (colour, number); Okey marks a real okey. Ids are the list positions + 100.</summary>
        private static (int[][] Layout, Dictionary<int, (int, int)?> Faces) Arrange(bool runs, params (int Color, int Number)[] hand)
        {
            var faces = new Dictionary<int, (int, int)?>();
            for (int i = 0; i < hand.Length; i++) faces[100 + i] = hand[i].Color == Okey ? ((int, int)?)null : (hand[i].Color, hand[i].Number);
            return (RackArranger.Arrange(faces.Keys, id => faces[id], runs), faces);
        }

        private static string Row(int[][] layout, Dictionary<int, (int, int)?> faces, int row) =>
            string.Join(" ", layout[row].Select(id => id < 0 ? "_" : faces[id] is (int c, int n) ? "RBYK"[c] + n.ToString() : "J"));

        [Test]
        public void SeriPicksTheHighestScoringSetsAndPutsTheOkeyWhereItScoresMost()
        {
            // R1-R3 (6), 7s in blue/yellow/black (21), and the okey as Y11 before Y12 Y13 (36): worth more than a 4th 7.
            var (layout, faces) = Arrange(true, (Y, 13), (R, 2), (K, 7), (Okey, 0), (R, 1), (B, 7), (Y, 12), (R, 3), (Y, 7));
            Assert.That(Row(layout, faces, 0), Is.EqualTo("R1 R2 R3 _ B7 Y7 K7 _ J Y12 Y13 _ _ _ _"));
            Assert.That(Row(layout, faces, 1), Is.EqualTo(string.Join(" ", Enumerable.Repeat("_", 15))));
        }

        [Test]
        public void SeriFillsTheMostValuableGap()
        {
            // The okey as R12 joins R10 R11 _ R13 into 10–13 (46) rather than R9 before R10 R11 (30).
            var (layout, faces) = Arrange(true, (R, 13), (Okey, 0), (R, 10), (R, 11));
            Assert.That(Row(layout, faces, 0), Does.StartWith("R10 R11 J R13 _"));
        }

        [Test]
        public void SeriNeverWrapsThirteenToOneAndGroupsNeedThreeColours()
        {
            var (layout, faces) = Arrange(true, (K, 12), (K, 13), (K, 1), (R, 5), (R, 5), (B, 5));
            // No set: everything is a leftover, bottom row first, in colour / number order.
            Assert.That(Row(layout, faces, 1), Does.StartWith("R5 R5 B5 K1 K12 K13 _"));
            Assert.That(layout[0].All(id => id < 0), Is.True);
        }

        [Test]
        public void LongerSetsGoFirstAndEachRowKeepsItsSetsInOrder()
        {
            // A 5-run, two groups and a 3-run: 5 + 3 + 3 fill the top row (13 cells with gaps), the last set goes below;
            // in the row the sets keep colour / number order (the red-led groups before the blue run).
            var (layout, faces) = Arrange(true,
                (B, 1), (B, 2), (B, 3), (B, 4), (B, 5),
                (R, 9), (Y, 9), (K, 9),
                (R, 11), (B, 11), (Y, 11),
                (Y, 3), (Y, 4), (Y, 5),
                (K, 2));
            Assert.That(Row(layout, faces, 0), Is.EqualTo("R9 Y9 K9 _ R11 B11 Y11 _ B1 B2 B3 B4 B5 _ _"));
            Assert.That(Row(layout, faces, 1), Is.EqualTo("Y3 Y4 Y5 _ K2 _ _ _ _ _ _ _ _ _ _"));
        }

        [Test]
        public void CiftPairsTwinsThenGivesOkeysToTheLargestSingles()
        {
            var (layout, faces) = Arrange(false, (B, 9), (R, 3), (K, 2), (Okey, 0), (Y, 13), (R, 3), (B, 9), (Okey, 0), (Y, 6));
            // Pairs R3, B9, Y13+okey, Y6+okey in colour / number order; the 2 left over.
            Assert.That(Row(layout, faces, 0), Is.EqualTo("R3 R3 _ B9 B9 _ Y6 J _ Y13 J _ _ _ _"));
            Assert.That(Row(layout, faces, 1), Does.StartWith("K2 _"));
        }

        [Test]
        public void CiftPairsTwoOkeysWhenNoSingleIsLeft()
        {
            var (layout, faces) = Arrange(false, (Okey, 0), (R, 3), (Okey, 0), (R, 3));
            Assert.That(Row(layout, faces, 0), Does.StartWith("R3 R3 _ J J _"));
        }

        [Test]
        public void CiftNeverFillsTheGapsBetweenPairs()
        {
            // Ten pairs and two singles (22 tiles) cannot all stand apart on 2 × 15: the most that can is eight pairs, the
            // rest go after them, and no single lands in a gap between two pairs.
            var hand = Enumerable.Range(1, 10).SelectMany(n => new[] { (R, n), (R, n) }).Concat(new[] { (B, 12), (K, 13) }).ToArray();
            var (layout, faces) = Arrange(false, hand);
            int pairs = 0;
            foreach (var row in layout)
                for (int x = 0; x < row.Length; x++)
                {
                    if (row[x] < 0 || (x > 0 && row[x - 1] >= 0)) continue;
                    int end = x; while (end + 1 < row.Length && row[end + 1] >= 0) end++;
                    if (end == x + 1 && faces[row[x]].Equals(faces[row[end]])) pairs++;
                }
            Assert.That(pairs, Is.EqualTo(8), Row(layout, faces, 0) + " / " + Row(layout, faces, 1));
        }

        [Test]
        public void EveryTileStaysOnTheRackAndBigHandsArrangeQuickly()
        {
            var random = new System.Random(101);
            var watch = Stopwatch.StartNew(); int slowest = 0;
            for (int round = 0; round < 300; round++)
            {
                var deck = new List<(int, int)>();
                for (int c = 0; c < 4; c++) for (int n = 1; n <= 13; n++) { deck.Add((c, n)); deck.Add((c, n)); }
                deck.Add((Okey, 0)); deck.Add((Okey, 0));
                var hand = deck.OrderBy(_ => random.Next()).Take(22).ToArray();
                var single = Stopwatch.StartNew();
                foreach (bool runs in new[] { true, false })
                {
                    var (layout, faces) = Arrange(runs, hand);
                    var placed = layout[0].Concat(layout[1]).Where(id => id >= 0).OrderBy(id => id).ToArray();
                    Assert.That(placed, Is.EqualTo(faces.Keys.OrderBy(id => id).ToArray()), "every tile placed once");
                }
                slowest = System.Math.Max(slowest, (int)single.ElapsedMilliseconds);
            }
            Assert.That(slowest, Is.LessThan(250), "one hand arranges at once (slowest " + slowest + " ms, total " + watch.ElapsedMilliseconds + " ms)");
        }
    }
}
