using System;
using System.Collections.Generic;
using System.Linq;

namespace Galaxy101.Table
{
    /// <summary>A rack position: row 0 = top, 1 = bottom (slotLocal r), index 0…14 left → right.</summary>
    public readonly struct RackSlot : IEquatable<RackSlot>
    {
        public readonly int Row, Index;
        public RackSlot(int row, int index) { Row = row; Index = index; }
        public bool Equals(RackSlot other) => Row == other.Row && Index == other.Index;
        public override bool Equals(object obj) => obj is RackSlot other && Equals(other);
        public override int GetHashCode() => Row * 32 + Index;
        public override string ToString() => Row + ":" + Index;
    }

    /// <summary>
    /// My rack: 2 × 15 cells holding tile ids (spec/11). Placement rules are ported from table3d.js
    /// (singlePlan, groupPlan + planRow) and the prototype component (place, firstFree, drawFrom auto-sort, sortBy).
    /// Pure data: no Unity types, so the rules are unit-tested directly.
    /// </summary>
    public sealed class RackGrid
    {
        public const int Columns = 15, Empty = -1;
        private readonly int[][] rows = { Fill(), Fill() };

        /// <summary>Tile face for placement heuristics: colour 0–3 in R, B, Y, K order and number 1–13; null for jokers.</summary>
        public delegate (int Color, int Number)? FaceOf(int id);

        private static int[] Fill() { var row = new int[Columns]; for (int i = 0; i < Columns; i++) row[i] = Empty; return row; }

        public int this[int row, int index] => rows[row][index];
        public int this[RackSlot slot] => rows[slot.Row][slot.Index];
        public int Count => rows[0].Count(x => x != Empty) + rows[1].Count(x => x != Empty);
        public IEnumerable<int> Tiles => rows[0].Concat(rows[1]).Where(x => x != Empty);

        public RackGrid Clone() { var copy = new RackGrid(); for (int r = 0; r < 2; r++) Array.Copy(rows[r], copy.rows[r], Columns); return copy; }

        public bool TryFind(int id, out RackSlot slot)
        {
            for (int r = 0; r < 2; r++) { int i = Array.IndexOf(rows[r], id); if (i >= 0) { slot = new RackSlot(r, i); return true; } }
            slot = default; return false;
        }

        public void Clear() { for (int r = 0; r < 2; r++) for (int i = 0; i < Columns; i++) rows[r][i] = Empty; }
        public void Set(RackSlot slot, int id) => rows[slot.Row][slot.Index] = id;
        public void Remove(int id) { if (TryFind(id, out var s)) rows[s.Row][s.Index] = Empty; }

        /// <summary>Every tile's slot, for applying or previewing a plan.</summary>
        public Dictionary<int, RackSlot> Placement()
        {
            var place = new Dictionary<int, RackSlot>();
            for (int r = 0; r < 2; r++) for (int i = 0; i < Columns; i++) if (rows[r][i] != Empty) place[rows[r][i]] = new RackSlot(r, i);
            return place;
        }

        /// <summary>Overwrite the grid with a plan (the preview the player saw is exactly what is committed).</summary>
        public void Apply(IReadOnlyDictionary<int, RackSlot> plan)
        {
            var moved = new HashSet<int>(plan.Keys);
            for (int r = 0; r < 2; r++) for (int i = 0; i < Columns; i++) if (moved.Contains(rows[r][i])) rows[r][i] = Empty;
            foreach (var pair in plan)
            {
                if (rows[pair.Value.Row][pair.Value.Index] != Empty) throw new InvalidOperationException("Rack plan overlaps slot " + pair.Value);
                rows[pair.Value.Row][pair.Value.Index] = pair.Key;
            }
        }

        // ---------- single tile (singlePlan) ----------
        /// <summary>
        /// Drop <paramref name="id"/> (already on the rack) at <paramref name="to"/>: the occupied target pushes its
        /// neighbours towards the nearer free cell (ties go right); a full row sends the displaced tile to the other
        /// row's nearest free cell. Never swaps, never overlaps.
        /// </summary>
        public Dictionary<int, RackSlot> SinglePlan(int id, RackSlot to)
        {
            var grid = Clone(); grid.Remove(id);
            int[] row = grid.rows[to.Row]; int i = to.Index;
            if (row[i] != Empty)
            {
                int left = -1, right = -1;
                for (int k = i - 1; k >= 0; k--) if (row[k] == Empty) { left = k; break; }
                for (int k = i + 1; k < Columns; k++) if (row[k] == Empty) { right = k; break; }
                bool goRight = right >= 0 && (left < 0 || right - i <= i - left);
                if (goRight) { for (int k = right; k > i; k--) row[k] = row[k - 1]; }
                else if (left >= 0) { for (int k = left; k < i; k++) row[k] = row[k + 1]; }
                else
                {
                    int[] other = grid.rows[1 - to.Row]; int best = -1, bestDistance = 99;
                    for (int k = 0; k < Columns; k++) if (other[k] == Empty && Math.Abs(k - i) < bestDistance) { bestDistance = Math.Abs(k - i); best = k; }
                    if (best >= 0) other[best] = row[i];
                }
            }
            row[i] = id;
            return grid.Placement();
        }

        // ---------- per group (groupPlan + planRow) ----------
        /// <summary>
        /// Move a contiguous group (ordered left → right, <paramref name="grabbed"/> = index of the held tile) so the held
        /// tile lands near <paramref name="to"/>. One free cell is kept on each side unless at the rack edge; the other
        /// tiles of that row keep their order and are pushed left/right with the cheapest split; overflow goes to the
        /// other row's nearest free cells.
        /// </summary>
        public Dictionary<int, RackSlot> GroupPlan(IReadOnlyList<int> members, int grabbed, RackSlot to)
        {
            int n = members.Count;
            var grid = Clone(); foreach (var id in members) grid.Remove(id);
            int r = to.Row, o2 = 1 - r, st = Math.Max(0, Math.Min(Columns - n, to.Index - grabbed));
            var row = new List<(int Id, int I)>();
            for (int i = 0; i < Columns; i++) if (grid.rows[r][i] != Empty) row.Add((grid.rows[r][i], i));
            var result = PlanRow(row, st, n);
            var place = new Dictionary<int, RackSlot>();
            foreach (var pair in result.Place) place[pair.Key] = new RackSlot(r, pair.Value);
            for (int k = 0; k < n; k++) place[members[k]] = new RackSlot(r, st + k);
            var other = (int[])grid.rows[o2].Clone();
            for (int i = 0; i < Columns; i++) if (other[i] != Empty) place[other[i]] = new RackSlot(o2, i);
            foreach (var x in result.Over.OrderBy(x => x.I))
            {
                int best = -1, bestDistance = 99;
                for (int i = 0; i < Columns; i++) if (other[i] == Empty && Math.Abs(i - x.I) < bestDistance) { bestDistance = Math.Abs(i - x.I); best = i; }
                if (best >= 0) { other[best] = x.Id; place[x.Id] = new RackSlot(o2, best); }
            }
            return place;
        }

        private sealed class RowPlan { public int Score; public Dictionary<int, int> Place; public List<(int Id, int I)> Over; }

        private static RowPlan PlanRow(List<(int Id, int I)> row, int st, int n)
        {
            int[][] tries = { new[] { 1, 1 }, new[] { 1, 0 }, new[] { 0, 1 }, new[] { 0, 0 } };
            RowPlan best = null;
            foreach (var t in tries)
            {
                int bl = t[0], br = t[1], en = st + n - 1;
                int leftLimit = st > 0 ? st - 1 - bl : -1, rightLimit = en < Columns - 1 ? en + 1 + br : Columns;
                for (int k = 0; k <= row.Count; k++)
                {
                    var place = new Dictionary<int, int>(); var over = new List<(int Id, int I)>(); int cost = 0, lim = leftLimit;
                    for (int j = k - 1; j >= 0; j--) { int p = Math.Min(row[j].I, lim); if (p < 0) { over.Add(row[j]); continue; } place[row[j].Id] = p; cost += row[j].I - p; lim = p - 1; }
                    lim = rightLimit;
                    for (int j = k; j < row.Count; j++) { int p = Math.Max(row[j].I, lim); if (p > Columns - 1) { over.Add(row[j]); continue; } place[row[j].Id] = p; cost += p - row[j].I; lim = p + 1; }
                    int split = k > 0 && k < row.Count && row[k - 1].I + 1 == row[k].I ? 60 : 0;
                    int score = over.Count * 1000 + (2 - bl - br) * 100 + split + cost;
                    if (best == null || score < best.Score) best = new RowPlan { Score = score, Place = place, Over = over };
                }
                if (best != null && best.Over.Count == 0 && best.Score < 100) break;
            }
            return best;
        }

        // ---------- new tiles (place, firstFree, drawFrom) ----------
        /// <summary>First free cell, bottom row first.</summary>
        public RackSlot? FirstFree()
        {
            foreach (int r in new[] { 1, 0 }) { int i = Array.IndexOf(rows[r], Empty); if (i >= 0) return new RackSlot(r, i); }
            return null;
        }

        /// <summary>Insert a new tile at <paramref name="at"/>, pushing towards the first free cell to the right, else left, else the other row.</summary>
        public bool Insert(int id, RackSlot at)
        {
            int[] row = rows[at.Row]; int i = at.Index;
            if (row[i] == Empty) { row[i] = id; return true; }
            int e = Array.IndexOf(row, Empty, i);
            if (e == -1)
            {
                e = Array.LastIndexOf(row, Empty, i);
                if (e == -1) { int[] other = rows[1 - at.Row]; int k = Array.IndexOf(other, Empty); if (k == -1) return false; other[k] = id; return true; }
                for (int x = e; x < i; x++) row[x] = row[x + 1];
                row[i] = id; return true;
            }
            for (int x = e; x > i; x--) row[x] = row[x - 1];
            row[i] = id; return true;
        }

        /// <summary>Auto-place (spec/10 §3): next to a fitting tile (same colour ±1, or same number other colour), else first free.</summary>
        public RackSlot? AutoSlot(int id, FaceOf face, bool autoSort)
        {
            var mine = face(id);
            if (autoSort && mine.HasValue)
                for (int r = 0; r < 2; r++)
                    for (int i = 0; i < Columns; i++)
                    {
                        if (rows[r][i] == Empty) continue;
                        var other = face(rows[r][i]); if (!other.HasValue) continue;
                        bool sameColour = other.Value.Color == mine.Value.Color;
                        if (sameColour && Math.Abs(other.Value.Number - mine.Value.Number) == 1 || other.Value.Number == mine.Value.Number && !sameColour)
                            return new RackSlot(r, sameColour && mine.Value.Number < other.Value.Number ? i : Math.Min(Columns - 1, i + 1));
                    }
            return FirstFree();
        }

        public bool Add(int id, FaceOf face, bool autoSort, RackSlot? at = null)
        {
            var slot = at ?? AutoSlot(id, face, autoSort);
            return slot.HasValue && Insert(id, slot.Value);
        }

        /// <summary>Keep the player's arrangement: drop tiles that left the hand, auto-place new ones.</summary>
        public void Sync(IEnumerable<int> hand, FaceOf face, bool autoSort)
        {
            var set = new HashSet<int>(hand);
            for (int r = 0; r < 2; r++) for (int i = 0; i < Columns; i++) if (rows[r][i] != Empty && !set.Contains(rows[r][i])) rows[r][i] = Empty;
            foreach (var id in set.OrderBy(x => x)) if (!TryFind(id, out _)) Add(id, face, autoSort);
        }

        /// <summary>Initial deal: tiles in dealt order, 11 per row from index 2 (top row first), like a freshly dealt rack.</summary>
        public void Deal(IReadOnlyList<int> hand)
        {
            Clear();
            for (int k = 0; k < hand.Count; k++) { int r = k < 11 ? 0 : 1, i = (k % 11) + 2; if (k >= 22) { var free = FirstFree(); if (free.HasValue) Set(free.Value, hand[k]); continue; } rows[r][i] = hand[k]; }
        }

        /// <summary>Contiguous tile runs per row (a meld on the rack is a gap-free neighbourhood).</summary>
        public List<RackSegment> Segments()
        {
            var list = new List<RackSegment>();
            for (int r = 0; r < 2; r++)
            {
                int i = 0;
                while (i < Columns)
                {
                    if (rows[r][i] == Empty) { i++; continue; }
                    int j = i; while (j < Columns && rows[r][j] != Empty) j++;
                    list.Add(new RackSegment(r, i, rows[r].Skip(i).Take(j - i).ToArray()));
                    i = j;
                }
            }
            return list;
        }

        /// <summary>sortBy(): runs = colour then number, pairs = number then colour; a gap between breaks, jokers last.</summary>
        public void Sort(bool runs, FaceOf face)
        {
            var tiles = Tiles.ToList();
            int Key(int id, bool primaryColour) { var f = face(id); if (!f.HasValue) return 1000; return primaryColour ? f.Value.Color * 100 + f.Value.Number : f.Value.Number * 10 + f.Value.Color; }
            tiles = tiles.OrderBy(t => Key(t, runs)).ThenBy(t => t).ToList();
            var output = new List<int>();
            for (int x = 0; x < tiles.Count; x++)
            {
                if (x > 0)
                {
                    var p = face(tiles[x - 1]); var c = face(tiles[x]);
                    bool brk = !p.HasValue || !c.HasValue ? p.HasValue != c.HasValue
                        : runs ? p.Value.Color != c.Value.Color || c.Value.Number != p.Value.Number + 1 : p.Value.Number != c.Value.Number;
                    if (brk) output.Add(Empty);
                }
                output.Add(tiles[x]);
            }
            while (output.Count > 2 * Columns) { int ix = output.LastIndexOf(Empty); if (ix < 0) break; output.RemoveAt(ix); }
            while (output.Count < 2 * Columns) output.Add(Empty);
            for (int k = 0; k < 2 * Columns; k++) rows[k / Columns][k % Columns] = output[k];
        }
    }

    public sealed class RackSegment
    {
        public readonly int Row, Start;
        public readonly int[] Ids;
        public RackSegment(int row, int start, int[] ids) { Row = row; Start = start; Ids = ids; }
    }
}
