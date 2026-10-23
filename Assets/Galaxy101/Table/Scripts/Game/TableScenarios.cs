using System;
using System.Collections.Generic;
using System.Linq;
using Okey101.Core.Model;

namespace Galaxy101.Table
{
    /// <summary>
    /// Prepared hands for the developer test menu and the acceptance captures (spec/25). Each returns a legal round state
    /// (no face beyond two copies) that <see cref="TableGame.Configure"/> plays from.
    /// </summary>
    public static class TableScenarios
    {
        public static readonly string[] Names = { "processing", "shortOpening", "fromHand", "opening" };

        public static RoundState Create(string name)
        {
            switch (name)
            {
                case "processing": return Processing();
                case "shortOpening": return ShortOpening();
                case "fromHand": return FromHand();
                case "opening": return Opening();
                default: throw new ArgumentException("Unknown scenario: " + name, nameof(name));
            }
        }

        private const TileColor R = TileColor.Red, Y = TileColor.Yellow, B = TileColor.Blue, K = TileColor.Black;
        public static int Id(TileColor c, int n, int copy = 0) => 2 * (13 * (int)c + n - 1) + copy;

        private sealed class Builder
        {
            private readonly HashSet<int> used = new HashSet<int>();
            private readonly List<Meld> melds = new List<Meld>();
            private readonly TileFace indicatorFace;
            public readonly int Indicator;
            public Builder(TileColor c, int n) { Indicator = Take(c, n); indicatorFace = new TileFace(c, n); }

            public int Take(TileColor c, int n)
            {
                for (int copy = 0; copy < 2; copy++) { int id = Id(c, n, copy); if (used.Add(id)) return id; }
                throw new InvalidOperationException("Both copies of " + c + " " + n + " are used.");
            }

            private PlacedTile Placed(int id) => new PlacedTile(new TileId(id), TileCatalog.IsRealJoker(new TileId(id), indicatorFace) ? throw new InvalidOperationException() : TileCatalog.GetNonJokerFace(new TileId(id), indicatorFace));

            public void Run(int owner, TileColor c, int from, int to, int okeyAt = -1)
            {
                var tiles = new List<PlacedTile>();
                for (int n = from; n <= to; n++)
                {
                    if (n == okeyAt) { var okey = TileCatalog.JokerFace(indicatorFace); tiles.Add(new PlacedTile(new TileId(Take(okey.Color, okey.Number)), new TileFace(c, n))); }
                    else tiles.Add(Placed(Take(c, n)));
                }
                melds.Add(new Meld(melds.Count + 1, owner, new MeldProposal(MeldKind.Run, tiles)));
            }

            public void Set(int owner, int n, params TileColor[] colors) =>
                melds.Add(new Meld(melds.Count + 1, owner, new MeldProposal(MeldKind.Set, colors.Select(c => Placed(Take(c, n))).ToList())));

            public int[] Hand(params (TileColor C, int N)[] faces) => faces.Select(f => Take(f.C, f.N)).ToArray();

            /// <summary>Unused tiles, fake okeys included, in a fixed shuffled order.</summary>
            public Queue<int> Rest(int seed)
            {
                var rng = new Random(seed);
                return new Queue<int>(Enumerable.Range(0, 106).Where(id => !used.Contains(id)).OrderBy(_ => rng.Next()));
            }

            public RoundState State(int[][] hands, OpeningKind[] openings, int active, RoundPhase phase, Queue<int> stock, int[][] discards = null, bool initial = false)
            {
                foreach (var h in hands) foreach (var id in h) used.Add(id);
                var players = Enumerable.Range(0, 4).Select(k => new PlayerState(hands[k].Select(id => new TileId(id)).ToArray(), openings[k], 0)).ToArray();
                var piles = Enumerable.Range(0, 4).Select(k => (IReadOnlyList<TileId>)(discards?[k] ?? new int[0]).Select(id => new TileId(id)).ToArray()).ToArray();
                var state = new RoundState(Guid.NewGuid(), initial ? 0 : 20, 7, active, active, initial ? 0 : 12, phase, new TileId(Indicator), stock.Select(id => new TileId(id)).ToArray(), players,
                    melds.ToArray(), piles, null, openings[active], 0, initial, initial ? new long[4] : new long[] { 5, 5, 5, 5 });
                var check = Okey101.Core.Turns.StateInvariant.Validate(state);
                if (!check.Success) throw new InvalidOperationException("Scenario breaks tile ownership: " + check.Error);
                return state;
            }
        }

        /// <summary>
        /// İşleme testi: everyone has opened, 15 melds on the board, hidden hands of 4 / 3 / 5. My rack: Y4 and Y8 (lay off
        /// on yellow runs / the 8s), B12 (takes the okey in B10-okey-B13), R13 (onto R10-12), K5 (onto the 5s or K1-4),
        /// B4 (onto B1-3), K13 (to discard). Indicator B6 → okey B7. My turn, draw first.
        /// </summary>
        private static RoundState Processing()
        {
            var b = new Builder(B, 6);
            var mine = b.Hand((Y, 4), (B, 12), (R, 13), (K, 5), (Y, 8), (B, 4), (K, 13));
            b.Run(0, R, 10, 12); b.Run(0, Y, 5, 7); b.Set(0, 5, R, Y, B);
            b.Run(1, B, 1, 3); b.Run(1, B, 10, 13, okeyAt: 12); b.Run(1, K, 7, 9); b.Run(1, Y, 11, 13);
            b.Run(2, R, 1, 4); b.Run(2, K, 10, 12); b.Set(2, 9, R, Y, B); b.Run(2, Y, 1, 3);
            b.Run(3, R, 6, 8); b.Run(3, K, 1, 4); b.Set(3, 8, R, B, K); b.Set(3, 11, R, B, K);
            var rest = b.Rest(25);
            int[] Hidden(int n) => Enumerable.Range(0, n).Select(_ => rest.Dequeue()).ToArray();
            var hands = new[] { mine, Hidden(4), Hidden(3), Hidden(5) };
            return b.State(hands, Enumerable.Repeat(OpeningKind.Series, 4).ToArray(), 0, RoundPhase.AwaitingDraw, rest);
        }

        /// <summary>Hatalı açma cezası: my sets add up to 66 (R3-5, 7s, B10-12) — Aç glows orange, pressing it costs +101.</summary>
        private static RoundState ShortOpening()
        {
            var b = new Builder(K, 1);
            var mine = b.Hand((R, 3), (R, 4), (R, 5), (K, 7), (B, 7), (Y, 7), (B, 10), (B, 11), (B, 12), (Y, 1), (K, 9), (R, 13), (Y, 12), (B, 2), (K, 4), (Y, 9), (R, 8), (B, 5), (K, 12), (Y, 3), (R, 1));
            var rest = b.Rest(66);
            int[] Hidden(int n) => Enumerable.Range(0, n).Select(_ => rest.Dequeue()).ToArray();
            var hands = new[] { mine, Hidden(21), Hidden(21), Hidden(21) };
            return b.State(hands, Enumerable.Repeat(OpeningKind.None, 4).ToArray(), 0, RoundPhase.AwaitingDraw, rest);
        }

        /// <summary>El açma: my first turn with 22 tiles, R10-13 · 11s · Y7-9 = 103 ready to open.</summary>
        private static RoundState Opening()
        {
            var b = new Builder(K, 4);
            var mine = b.Hand((R, 10), (R, 11), (R, 12), (R, 13), (K, 11), (B, 11), (Y, 11), (Y, 7), (Y, 8), (Y, 9),
                (B, 2), (K, 7), (R, 3), (Y, 1), (B, 6), (K, 9), (R, 6), (B, 13), (Y, 4), (K, 1), (B, 8), (R, 8));
            var rest = b.Rest(101);
            int[] Hidden(int n) => Enumerable.Range(0, n).Select(_ => rest.Dequeue()).ToArray();
            var hands = new[] { mine, Hidden(21), Hidden(21), Hidden(21) };
            return b.State(hands, Enumerable.Repeat(OpeningKind.None, 4).ToArray(), 0, RoundPhase.PlanningTurn, rest, initial: true);
        }

        /// <summary>Elden bitiş: I start with 22, never opened; 21 sit in sets and K8 is the spare — Elden bit.</summary>
        private static RoundState FromHand()
        {
            var b = new Builder(K, 1);
            var mine = b.Hand((R, 1), (R, 2), (R, 3), (Y, 6), (Y, 7), (Y, 8), (Y, 9), (B, 9), (B, 10), (B, 11), (B, 12), (K, 5), (R, 5), (Y, 5), (B, 5),
                (B, 13), (K, 13), (R, 13), (Y, 1), (Y, 2), (Y, 3), (K, 8));
            var rest = b.Rest(14);
            int[] Hidden(int n) => Enumerable.Range(0, n).Select(_ => rest.Dequeue()).ToArray();
            var hands = new[] { mine, Hidden(21), Hidden(21), Hidden(21) };
            return b.State(hands, Enumerable.Repeat(OpeningKind.None, 4).ToArray(), 0, RoundPhase.PlanningTurn, rest, initial: true);
        }
    }
}
