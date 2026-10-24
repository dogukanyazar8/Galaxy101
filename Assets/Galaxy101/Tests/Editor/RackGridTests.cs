using System.Collections.Generic;
using System.Linq;
using Galaxy101.Table;
using NUnit.Framework;
using Okey101.Core.Model;

namespace Galaxy101.Tests
{
    /// <summary>Rack placement rules (spec/11), including the §7 per-group scenarios.</summary>
    public sealed class RackGridTests
    {
        // Letters are ids: A=0 … L=11 on the top row, m=20 … v=28 on the bottom row.
        private static RackGrid Spec7()
        {
            var rack = new RackGrid();
            string top = "ABCD.EFG.HIJ.KL", bottom = "mn.opq.r.st.uv.";
            for (int i = 0; i < 15; i++)
            {
                if (top[i] != '.') rack.Set(new RackSlot(0, i), top[i] - 'A');
                if (bottom[i] != '.') rack.Set(new RackSlot(1, i), 20 + bottom[i] - 'm');
            }
            return rack;
        }

        private static string Row(RackGrid rack, int row) => new string(Enumerable.Range(0, 15).Select(i => rack[row, i] < 0 ? '.' : rack[row, i] >= 20 ? (char)('m' + rack[row, i] - 20) : (char)('A' + rack[row, i])).ToArray());

        private static void AssertSound(RackGrid before, RackGrid after)
        {
            Assert.That(after.Count, Is.EqualTo(before.Count), "no tile lost or duplicated");
            Assert.That(after.Tiles.Distinct().Count(), Is.EqualTo(after.Count));
        }

        [Test]
        public void GroupToTopSlotTwoKeepsTheBlockTogether()
        {
            var rack = Spec7(); var before = rack.Clone();
            rack.Apply(rack.GroupPlan(new[] { 7, 8, 9 }, 0, new RackSlot(0, 2)));
            AssertSound(before, rack);
            string top = Row(rack, 0);
            int h = top.IndexOf('H');
            Assert.That(top.Substring(h, 3), Is.EqualTo("HIJ"));
            Assert.That(top.Replace(".", "").Replace("HIJ", ""), Is.EqualTo("ABCDEFGKL"), "pushed tiles keep their order");
            Assert.That(top.Contains("ABCD"), Is.True, "the A-B-C-D neighbourhood is not split");
            if (h > 0) Assert.That(top[h - 1], Is.EqualTo('.')); if (h + 3 < 15) Assert.That(top[h + 3], Is.EqualTo('.'));
            Assert.That(Row(rack, 1), Is.EqualTo(Row(before, 1)), "nothing spills to the bottom row");
        }

        [Test]
        public void GroupIntoAFullRowGivesUpOneGapAndDoesNotOverflow()
        {
            var rack = Spec7(); var before = rack.Clone();
            rack.Apply(rack.GroupPlan(new[] { 0, 1, 2, 3 }, 0, new RackSlot(1, 6)));
            AssertSound(before, rack);
            string bottom = Row(rack, 1);
            Assert.That(bottom.Contains("ABCD"), Is.True);
            Assert.That(bottom.Replace(".", "").Replace("ABCD", ""), Is.EqualTo("mnopqrstuv"));
            Assert.That(Row(rack, 0).Replace(".", ""), Is.EqualTo("EFGHIJKL"), "no bottom tile overflows to the top row");
        }

        [Test]
        public void GroupAtTheEdgeKeepsOnlyTheInnerGap()
        {
            var rack = Spec7(); var before = rack.Clone();
            rack.Apply(rack.GroupPlan(new[] { 4, 5, 6 }, 0, new RackSlot(0, 0)));
            AssertSound(before, rack);
            string top = Row(rack, 0);
            Assert.That(top.Substring(0, 4), Is.EqualTo("EFG."));
            Assert.That(top.Substring(4, 4), Is.EqualTo("ABCD"));
        }

        [Test]
        public void SingleDropPushesTowardsTheNearerGapWithoutSwapping()
        {
            var rack = Spec7();
            // L (11) onto B's slot (1): the left gap is far (none), the nearest free cell right of 1 is 4.
            rack.Apply(rack.SinglePlan(11, new RackSlot(0, 1)));
            Assert.That(Row(rack, 0), Is.EqualTo("ALBCDEFG.HIJ.K."));
        }

        [Test]
        public void SingleDropIntoAFullRowSendsTheDisplacedTileAcross()
        {
            var rack = new RackGrid();
            for (int i = 0; i < 15; i++) rack.Set(new RackSlot(0, i), i);
            rack.Set(new RackSlot(1, 0), 20);
            rack.Apply(rack.SinglePlan(20, new RackSlot(0, 7)));
            Assert.That(rack[0, 7], Is.EqualTo(20));
            Assert.That(rack.TryFind(7, out var moved) && moved.Row == 1, Is.True, "the displaced tile goes to the other row");
            Assert.That(rack.Count, Is.EqualTo(16));
        }

        [Test]
        public void InsertAndAutoPlacementFollowThePrototype()
        {
            var rack = new RackGrid();
            Assert.That(rack.FirstFree(), Is.EqualTo(new RackSlot(1, 0)), "bottom row first");
            var faces = new Dictionary<int, (int, int)?> { [1] = (0, 5), [2] = (0, 7), [3] = (0, 6), [4] = (2, 9) };
            RackGrid.FaceOf face = id => faces.TryGetValue(id, out var f) ? f : null;
            rack.Set(new RackSlot(0, 3), 1); rack.Set(new RackSlot(0, 4), 2);
            // R6 fits next to R5 / R7: inserted after R5 (same colour, higher number) and R7 shifts right.
            rack.Add(3, face, true);
            Assert.That(new[] { rack[0, 3], rack[0, 4], rack[0, 5] }, Is.EqualTo(new[] { 1, 3, 2 }));
            rack.Add(4, face, true);
            Assert.That(rack[1, 0], Is.EqualTo(4), "no match → first free cell");
        }

        [Test]
        public void SortLaysTheBestSetsOutWithTheRestBelow()
        {
            var rack = new RackGrid();
            var faces = new Dictionary<int, (int, int)?> { [1] = (1, 3), [2] = (0, 2), [3] = (0, 3), [4] = (1, 4), [5] = null };
            for (int i = 0; i < 5; i++) rack.Set(new RackSlot(1, i * 2), i + 1);
            rack.Sort(true, id => faces[id]);
            // The okey (5) completes B3 B4 as B5 (12, more than R2 R3 + R4 or the 3s); R2 R3 go to the bottom row.
            Assert.That(Enumerable.Range(0, 4).Select(i => rack[0, i]).ToArray(), Is.EqualTo(new[] { 1, 4, 5, -1 }));
            Assert.That(Enumerable.Range(0, 3).Select(i => rack[1, i]).ToArray(), Is.EqualTo(new[] { 2, 3, -1 }));
        }

        // Engine ids: 2 × (13 × colour + number − 1) + copy, colours Red, Yellow, Blue, Black.
        private static int Id(TileColor color, int number, int copy = 0) => 2 * (13 * (int)color + number - 1) + copy;

        [Test]
        public void OpeningMeterCountsGapFreeRunsSetsAndPairs()
        {
            var indicator = new TileFace(TileColor.Black, 13); // okey = black 1
            var rack = new RackGrid();
            int[] run = { Id(TileColor.Red, 10), Id(TileColor.Red, 11), Id(TileColor.Red, 12), Id(TileColor.Red, 13) };
            int[] set = { Id(TileColor.Red, 9), Id(TileColor.Yellow, 9), Id(TileColor.Blue, 9) };
            int[] pair = { Id(TileColor.Blue, 4), Id(TileColor.Blue, 4, 1) };
            for (int i = 0; i < run.Length; i++) rack.Set(new RackSlot(0, i), run[i]);
            for (int i = 0; i < set.Length; i++) rack.Set(new RackSlot(0, 6 + i), set[i]);
            for (int i = 0; i < pair.Length; i++) rack.Set(new RackSlot(1, i), pair[i]);
            rack.Set(new RackSlot(1, 5), Id(TileColor.Yellow, 2)); rack.Set(new RackSlot(1, 6), Id(TileColor.Blue, 7)); rack.Set(new RackSlot(1, 7), Id(TileColor.Red, 3));
            var eval = HandEvaluation.Of(rack, indicator);
            Assert.That(eval.Sum, Is.EqualTo(46 + 27));
            Assert.That(eval.Groups.Count, Is.EqualTo(2));
            Assert.That(eval.Pairs.Count, Is.EqualTo(1));
            Assert.That(eval.ValidTiles.SetEquals(run.Concat(set)), Is.True, "only valid segments light up");

            // The okey (black 1) completes a run as a wild tile.
            var wild = new RackGrid();
            wild.Set(new RackSlot(0, 0), Id(TileColor.Blue, 11)); wild.Set(new RackSlot(0, 1), Id(TileColor.Black, 1)); wild.Set(new RackSlot(0, 2), Id(TileColor.Blue, 13));
            Assert.That(HandEvaluation.Of(wild, indicator).Sum, Is.EqualTo(36));
        }

        [Test]
        public void OpeningMeterPicksWhatTheEngineCandidateOrderPicks()
        {
            // The meter builds a segment's group directly; it must equal the best whole-segment candidate of the engine's
            // generator (same start, same okey bindings) on runs, sets, okeys, fake okeys and strays.
            var random = new System.Random(7);
            for (int round = 0; round < 3000; round++)
            {
                var indicator = new TileFace((TileColor)random.Next(4), 1 + random.Next(13));
                var tiles = new List<int>(); int length = 3 + random.Next(random.Next(2) == 0 ? 2 : 11);
                if (random.Next(2) == 0) { int c = random.Next(4), start = 1 + random.Next(13); for (int k = 0; k < length && start + k <= 13; k++) tiles.Add(Id((TileColor)c, start + k, random.Next(2))); }
                else { int n = 1 + random.Next(13); foreach (var c in Enumerable.Range(0, 4).OrderBy(_ => random.Next()).Take(System.Math.Min(length, 4))) tiles.Add(Id((TileColor)c, n, random.Next(2))); }
                var joker = Okey101.Core.Model.TileCatalog.JokerFace(indicator);
                for (int j = random.Next(3); j > 0 && tiles.Count > 0; j--) { int wild = new[] { Id(joker.Color, joker.Number), Id(joker.Color, joker.Number, 1), 104, 105 }[random.Next(4)]; if (!tiles.Contains(wild)) tiles[random.Next(tiles.Count)] = wild; }
                tiles = tiles.Distinct().OrderBy(_ => random.Next()).ToList(); if (tiles.Count < 3) continue;
                var rack = new RackGrid(); for (int i = 0; i < tiles.Count; i++) rack.Set(new RackSlot(0, i), tiles[i]);

                var ids = tiles.Select(t => new TileId(t)).ToArray(); MeldProposal best = null; int bestValue = -1;
                foreach (var kind in new[] { MeldKind.Run, MeldKind.Set })
                    if (kind == MeldKind.Run || ids.Length <= 4)
                        foreach (var proposal in Okey101.Core.Rules.MeldCandidateGenerator.Generate(ids, indicator, kind))
                            if (proposal.Tiles.Count == ids.Length && Okey101.Core.Rules.MeldValidator.Validate(proposal, indicator) is var v && v.Success && v.Value > bestValue) { best = proposal; bestValue = v.Value; }
                string Key(MeldProposal g) => g == null ? "none" : g.Kind + ":" + string.Join(",", g.Tiles.Select(t => t.Id.Value + "/" + t.RepresentedFace));
                var eval = HandEvaluation.Of(rack, indicator);
                Assert.That(Key(eval.Groups.FirstOrDefault()), Is.EqualTo(Key(best)), string.Join(",", tiles) + " indicator " + indicator);
                Assert.That(eval.Sum, Is.EqualTo(best == null ? 0 : bestValue));
            }
        }
    }
}
