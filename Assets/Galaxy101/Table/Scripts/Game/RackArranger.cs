using System;
using System.Collections.Generic;
using System.Linq;

namespace Galaxy101.Table
{
    /// <summary>
    /// "Seri diz" / "Çift diz": the whole rack laid out again on its 2 × 15 cells. The okey (real okey tiles: no face) is
    /// a wild tile; a fake okey plays as the plain tile of the okey's colour and number. Order: red, blue, yellow, black,
    /// then 1 → 13.
    /// <list type="bullet">
    /// <item>Seri: runs (one colour, 3+ consecutive numbers, no 13 → 1) and groups (one number, 3–4 different colours)
    /// chosen so the tiles in them add up to the most; an okey counts the tile it stands for and goes to the gap worth
    /// most. Tiles outside the sets follow in colour/number order.</item>
    /// <item>Çift: two tiles of one colour and number make a pair; tiles without a twin, largest first, each take an okey;
    /// two okeys left with no single tile pair with each other. Pairs in colour/number order; the rest and a spare okey
    /// last.</item>
    /// <item>Placement: longer sets first, each into the first row with room (top first), one empty cell between sets;
    /// within a row the sets keep their own order left to right; leftover tiles fill the trailing cells of the bottom row,
    /// then of the top row, one empty cell after the sets. When the leftovers do not fit there, the least valuable set
    /// joins them (a gap between sets is never filled, so every set placed stays a set).</item>
    /// </list>
    /// Pure data (no Unity types), unit-tested directly.
    /// </summary>
    public static class RackArranger
    {
        public const int Columns = RackGrid.Columns, Empty = RackGrid.Empty;

        /// <summary>A laid-out set: its tile ids left to right and its place in colour/number order.</summary>
        public sealed class Set
        {
            public readonly List<int> Ids = new List<int>();
            public int Key;
            /// <summary>What the set is worth when the rack has no room for all of them: the sum of its numbers (seri), 0 (çift).</summary>
            public int Value;
        }

        /// <summary>The 2 × 15 layout of <paramref name="tiles"/> for "Seri diz" (<paramref name="runs"/>) or "Çift diz".</summary>
        public static int[][] Arrange(IEnumerable<int> tiles, RackGrid.FaceOf face, bool runs)
        {
            var all = tiles.ToList();
            var jokers = all.Where(id => !face(id).HasValue).OrderBy(id => id).ToList();
            var plain = all.Where(id => face(id).HasValue).OrderBy(id => Order(face(id).Value)).ThenBy(id => id).ToList();
            List<Set> sets; List<int> rest;
            if (runs) (sets, rest) = Series(plain, jokers, face);
            else (sets, rest) = Pairs(plain, jokers, face);
            var layout = Place(sets, rest);
            var placed = layout[0].Concat(layout[1]).Where(id => id != Empty).ToList();
            if (placed.Count != all.Count || placed.Except(all).Any()) throw new InvalidOperationException("Rack arrangement lost or invented a tile.");
            return layout;
        }

        private static int Order((int Color, int Number) f) => f.Color * 100 + f.Number;

        // ---------- seri ----------

        /// <summary>A set before ids are dealt out: each cell's colour and number and whether an okey fills it.</summary>
        private sealed class Plan
        {
            public readonly List<(int Color, int Number, bool Wild)> Cells;
            public readonly Plan Next;
            public Plan(List<(int, int, bool)> cells, Plan next) { Cells = cells; Next = next; }
        }

        private static (List<Set>, List<int>) Series(List<int> plain, List<int> jokers, RackGrid.FaceOf face)
        {
            var counts = new int[4, 14];
            var ids = new Dictionary<int, Queue<int>>();
            foreach (var id in plain)
            {
                var f = face(id).Value; counts[f.Color, f.Number]++;
                int key = Order(f); if (!ids.TryGetValue(key, out var q)) ids[key] = q = new Queue<int>(); q.Enqueue(id);
            }
            var memo = new Dictionary<string, (int Score, Plan Plan)>();
            var best = Best(counts, jokers.Count, memo);

            var sets = new List<Set>(); var wild = new Queue<int>(jokers);
            for (var plan = best.Plan; plan != null; plan = plan.Next)
            {
                var set = new Set();
                foreach (var (c, n, w) in plan.Cells) set.Ids.Add(w ? wild.Dequeue() : ids[c * 100 + n].Dequeue());
                var first = plan.Cells[0]; set.Key = first.Color * 100 + first.Number; set.Value = plan.Cells.Sum(x => x.Number); sets.Add(set);
            }
            var rest = ids.OrderBy(p => p.Key).SelectMany(p => p.Value).ToList(); rest.AddRange(wild);
            return (sets, rest);
        }

        /// <summary>
        /// The most the remaining tiles can score in sets. The first remaining tile (colour, then number) is either left
        /// out, or opens a group of its number, or a run of its colour (earlier cells of that run can only be okeys).
        /// </summary>
        private static (int Score, Plan Plan) Best(int[,] counts, int jokers, Dictionary<string, (int, Plan)> memo)
        {
            string key = Key(counts, jokers);
            if (memo.TryGetValue(key, out var known)) return known;
            int c0 = -1, n0 = -1;
            for (int c = 0; c < 4 && c0 < 0; c++) for (int n = 1; n <= 13; n++) if (counts[c, n] > 0) { c0 = c; n0 = n; break; }
            if (c0 < 0) { memo[key] = (0, null); return (0, null); }

            counts[c0, n0]--;
            var result = Best(counts, jokers, memo); // the tile stays out of every set

            // Groups: the tile's number in 3–4 different colours, each other colour a tile, an okey or absent.
            var others = Enumerable.Range(0, 4).Where(c => c != c0).ToArray();
            for (int mask = 0; mask < 27; mask++)
            {
                var choice = new int[3]; int m = mask, size = 1, wild = 0; bool ok = true;
                for (int k = 0; k < 3; k++) { choice[k] = m % 3; m /= 3; if (choice[k] == 1 && counts[others[k], n0] == 0) ok = false; if (choice[k] > 0) size++; if (choice[k] == 2) wild++; }
                if (!ok || size < 3 || wild > jokers) continue;
                for (int k = 0; k < 3; k++) if (choice[k] == 1) counts[others[k], n0]--;
                var after = Best(counts, jokers - wild, memo);
                for (int k = 0; k < 3; k++) if (choice[k] == 1) counts[others[k], n0]++;
                int score = after.Score + n0 * size;
                if (score > result.Score)
                {
                    var cells = new List<(int, int, bool)> { (c0, n0, false) };
                    for (int k = 0; k < 3; k++) if (choice[k] > 0) cells.Add((others[k], n0, choice[k] == 2));
                    result = (score, new Plan(cells.OrderBy(x => x.Item1).ToList(), after.Plan));
                }
            }

            // Runs of the tile's colour: okeys may stand for lower numbers (those tiles are already gone), higher numbers
            // take a tile or an okey.
            for (int start = n0; start >= 1 && n0 - start <= jokers; start--)
                Extend(counts, jokers - (n0 - start), c0, start, n0, n0, new List<(int, int, bool)>(Enumerable.Range(start, n0 - start).Select(n => (c0, n, true))) { (c0, n0, false) }, memo, ref result);

            counts[c0, n0]++;
            memo[key] = result; return result;
        }

        private static void Extend(int[,] counts, int jokers, int color, int start, int first, int end, List<(int, int, bool)> cells, Dictionary<string, (int, Plan)> memo, ref (int Score, Plan Plan) result)
        {
            if (end - start + 1 >= 3)
            {
                var after = Best(counts, jokers, memo);
                int score = after.Score + (start + end) * (end - start + 1) / 2;
                if (score > result.Score) result = (score, new Plan(new List<(int, int, bool)>(cells), after.Plan));
            }
            int next = end + 1; if (next > 13) return; // no 13 → 1
            if (counts[color, next] > 0)
            {
                counts[color, next]--; cells.Add((color, next, false));
                Extend(counts, jokers, color, start, first, next, cells, memo, ref result);
                cells.RemoveAt(cells.Count - 1); counts[color, next]++;
            }
            if (jokers > 0)
            {
                cells.Add((color, next, true));
                Extend(counts, jokers - 1, color, start, first, next, cells, memo, ref result);
                cells.RemoveAt(cells.Count - 1);
            }
        }

        private static string Key(int[,] counts, int jokers)
        {
            var chars = new char[53];
            for (int c = 0; c < 4; c++) for (int n = 1; n <= 13; n++) chars[c * 13 + n - 1] = (char)('0' + counts[c, n]);
            chars[52] = (char)('0' + jokers); return new string(chars);
        }

        // ---------- çift ----------

        private static (List<Set>, List<int>) Pairs(List<int> plain, List<int> jokers, RackGrid.FaceOf face)
        {
            var sets = new List<Set>(); var singles = new List<int>();
            foreach (var group in plain.GroupBy(id => Order(face(id).Value)).OrderBy(g => g.Key))
            {
                var list = group.ToList();
                for (int i = 0; i + 1 < list.Count; i += 2) { var set = new Set { Key = group.Key }; set.Ids.Add(list[i]); set.Ids.Add(list[i + 1]); sets.Add(set); }
                if (list.Count % 2 == 1) singles.Add(list[list.Count - 1]);
            }
            // Singles largest first, each taking an okey while okeys last.
            var wild = new Queue<int>(jokers);
            foreach (var id in singles.OrderByDescending(id => face(id).Value.Number).ThenBy(id => face(id).Value.Color).ToList())
            {
                if (wild.Count == 0) break;
                var set = new Set { Key = Order(face(id).Value) }; set.Ids.Add(id); set.Ids.Add(wild.Dequeue()); sets.Add(set); singles.Remove(id);
            }
            if (singles.Count == 0 && wild.Count >= 2) { var set = new Set { Key = int.MaxValue }; set.Ids.Add(wild.Dequeue()); set.Ids.Add(wild.Dequeue()); sets.Add(set); }
            var rest = singles.OrderBy(id => Order(face(id).Value)).ThenBy(id => id).ToList(); rest.AddRange(wild);
            return (sets.OrderBy(s => s.Key).ToList(), rest);
        }

        // ---------- placement ----------

        private static int[][] Place(List<Set> sets, List<int> rest)
        {
            var kept = sets.OrderByDescending(s => s.Ids.Count).ThenBy(s => s.Key).ToList();
            var dropped = new List<int>();
            while (true)
            {
                var rows = new[] { new List<Set>(), new List<Set>() }; var used = new int[2];
                var leftover = new List<int>(dropped);
                foreach (var set in kept)
                {
                    int len = set.Ids.Count; bool placed = false;
                    for (int r = 0; r < 2 && !placed; r++)
                    {
                        int need = used[r] == 0 ? len : used[r] + 1 + len;
                        if (need <= Columns) { rows[r].Add(set); used[r] = need; placed = true; }
                    }
                    if (!placed) leftover.AddRange(set.Ids);
                }
                int room = 0;
                for (int r = 0; r < 2; r++) room += used[r] == 0 ? Columns : Math.Max(0, Columns - used[r] - 1);
                var onRack = rows[0].Concat(rows[1]).ToList();
                if (leftover.Count + rest.Count > room && onRack.Count > 0)
                {
                    // Each set given up frees one cell more than its tiles fill, so this ends.
                    var drop = onRack.OrderBy(s => s.Value).ThenByDescending(s => s.Key).First();
                    kept.Remove(drop); dropped.AddRange(drop.Ids);
                    continue;
                }
                return Lay(rows, used, leftover.Concat(rest));
            }
        }

        private static int[][] Lay(List<Set>[] rows, int[] used, IEnumerable<int> loose)
        {
            var layout = new[] { Enumerable.Repeat(Empty, Columns).ToArray(), Enumerable.Repeat(Empty, Columns).ToArray() };
            for (int r = 0; r < 2; r++)
            {
                int x = 0;
                foreach (var set in rows[r].OrderBy(s => s.Key)) { if (x > 0) x++; foreach (var id in set.Ids) layout[r][x++] = id; }
            }
            // Leftovers: the bottom row's trailing cells, then the top row's (after a one-cell gap from the sets).
            var queue = new Queue<int>(loose);
            foreach (int r in new[] { 1, 0 })
            {
                int x = used[r] == 0 ? 0 : used[r] + 1;
                while (x < Columns && queue.Count > 0) layout[r][x++] = queue.Dequeue();
            }
            // Never lose a tile: anything still left takes the remaining empty cells.
            foreach (int r in new[] { 1, 0 }) for (int x = 0; x < Columns && queue.Count > 0; x++) if (layout[r][x] == Empty) layout[r][x] = queue.Dequeue();
            return layout;
        }
    }
}
