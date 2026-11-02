using System;
using System.Collections.Generic;
using System.Linq;

namespace Galaxy101.Table
{
    /// <summary>Canvas geometry from table2d.js. Tile identities and rack plans stay in the existing game model.</summary>
    public static class Table2DLayout
    {
        public readonly struct Box
        {
            public readonly float X, Y, Width, Height;
            public Box(float x, float y, float width, float height) { X = x; Y = y; Width = width; Height = height; }
            public bool Contains(float x, float y, float padding = 0) => x >= X - padding && x <= X + Width + padding && y >= Y - padding && y <= Y + Height + padding;
        }

        public sealed class Meld
        {
            public readonly int Id, Seat;
            public readonly IReadOnlyList<int> Tiles;
            public Meld(int id, int seat, IReadOnlyList<int> tiles)
            {
                if (seat < 0 || seat > 3) throw new ArgumentOutOfRangeException(nameof(seat));
                Id = id; Seat = seat; Tiles = tiles ?? throw new ArgumentNullException(nameof(tiles));
            }
        }

        public static readonly Box Discard = new Box(1076, 560, 64, 86), Side = new Box(148, 572, 56, 76), Deck = new Box(614, 254, 52, 72);
        public const float RackX = 234, RackY = 504, RackStep = 54, RackRow = 74;

        public static Box RackTile(RackSlot slot)
        {
            if (slot.Row < 0 || slot.Row > 1 || slot.Index < 0 || slot.Index >= RackGrid.Columns) throw new ArgumentOutOfRangeException(nameof(slot));
            return new Box(RackX + slot.Index * RackStep, RackY + slot.Row * RackRow, 50, 70);
        }

        public static bool TrySlot(float x, float y, out RackSlot slot)
        {
            slot = default;
            if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y) || !new Box(224, 490, 820, 158).Contains(x, y)) return false;
            slot = new RackSlot(y > RackY + RackRow - 6 ? 1 : 0, Math.Max(0, Math.Min(14, (int)Math.Round((x - RackX - 25) / RackStep, MidpointRounding.AwayFromZero))));
            return true;
        }

        public static Box PileTile(int seat, int index)
        {
            float[] x = { 1083, 1160, 478, 151 }, y = { 568, 362, 92, 575 };
            if (seat < 0 || seat > 3) throw new ArgumentOutOfRangeException(nameof(seat));
            float offset = Math.Min(Math.Max(0, index), 4) * 1.5f;
            return new Box(x[seat] + offset, y[seat] - offset, seat == 0 || seat == 3 ? 50 : 42, seat == 0 || seat == 3 ? 70 : 60);
        }

        public static Dictionary<int, Box> MeldTiles(IEnumerable<Meld> melds)
        {
            if (melds == null) throw new ArgumentNullException(nameof(melds));
            var result = new Dictionary<int, Box>();
            foreach (var group in melds.GroupBy(m => m.Seat))
            {
                int seat = group.Key;
                float left = seat == 3 ? 165 : seat == 1 ? 785 : seat == 2 ? 330 : 240;
                float right = seat == 3 ? 495 : seat == 1 ? 1115 : seat == 2 ? 950 : 1040;
                float[] rows = seat == 2 ? new float[] { 178, 216 } : seat == 0 ? new float[] { 356 } : new float[] { 232, 272, 312, 352 };
                var lines = new List<List<Meld>> { new List<Meld>() }; var widths = new List<float> { 0 };
                foreach (var meld in group.OrderBy(m => m.Tiles.Count == 2 ? 1 : 0))
                {
                    float width = meld.Tiles.Count * 26 - 2; int row = lines.Count - 1;
                    if (widths[row] > 0 && widths[row] + 12 + width > right - left && lines.Count < rows.Length)
                    { lines.Add(new List<Meld>()); widths.Add(0); row++; }
                    lines[row].Add(meld); widths[row] += (widths[row] > 0 ? 12 : 0) + width;
                }
                for (int row = 0; row < lines.Count; row++)
                {
                    float x = seat == 3 ? left : seat == 1 ? right - widths[row] : left + (right - left - widths[row]) / 2;
                    foreach (var meld in lines[row])
                    {
                        for (int j = 0; j < meld.Tiles.Count; j++) result.Add(meld.Tiles[j], new Box((float)Math.Round(x + j * 26), rows[row], 24, 34));
                        x += meld.Tiles.Count * 26 - 2 + 12;
                    }
                }
            }
            return result;
        }
    }
}
