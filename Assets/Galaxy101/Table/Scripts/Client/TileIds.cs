using System;
using System.Collections.Generic;

namespace Galaxy101.Table
{
    /// <summary>
    /// The one bijection between the v4 engine's string tile ids (D-01: colour + number + copy, "R5a", "R5b", fakes "Ja",
    /// "Jb") and the int ids the 3D table's objects, rack grid and board use (0–105: 2 × (13 × colour + n − 1) + copy with
    /// colours red, yellow, blue, black; the fakes 104 and 105). Every physical tile keeps its int id for the whole hand.
    /// </summary>
    public static class TileIds
    {
        /// <summary>Int colour order of the table's ids and face atlas codes.</summary>
        private static readonly string[] Letters = { "R", "Y", "B", "K" };
        private static readonly Dictionary<string, int> ToIntMap = new Dictionary<string, int>();
        private static readonly string[] ToV4Map = new string[106];

        static TileIds()
        {
            for (int c = 0; c < 4; c++)
                for (int n = 1; n <= 13; n++)
                    for (int copy = 0; copy < 2; copy++)
                    {
                        int id = 2 * (13 * c + n - 1) + copy;
                        string v4 = Letters[c] + n + (copy == 0 ? "a" : "b");
                        ToIntMap[v4] = id; ToV4Map[id] = v4;
                    }
            ToIntMap["Ja"] = 104; ToV4Map[104] = "Ja";
            ToIntMap["Jb"] = 105; ToV4Map[105] = "Jb";
        }

        /// <summary>The int id of a v4 tile id; -1 for null or an unknown id.</summary>
        public static int ToInt(string id) => id != null && ToIntMap.TryGetValue(id, out var v) ? v : -1;

        /// <summary>The v4 id of an int id (0–105).</summary>
        public static string ToV4(int id)
        {
            if (id < 0 || id >= 106) throw new ArgumentOutOfRangeException(nameof(id));
            return ToV4Map[id];
        }

        public static List<int> ToInts(IEnumerable<string> ids)
        {
            var list = new List<int>();
            if (ids != null) foreach (var id in ids) { int v = ToInt(id); if (v >= 0) list.Add(v); }
            return list;
        }

        public static List<string> ToV4s(IEnumerable<int> ids)
        {
            var list = new List<string>();
            if (ids != null) foreach (var id in ids) list.Add(ToV4(id));
            return list;
        }

        /// <summary>The face atlas code of an int id: "R7", "K13", or "joker" for the two fake okeys.</summary>
        public static string Code(int id)
        {
            if (id >= 104) return "joker";
            int c = id / 26, n = id % 26 / 2 + 1;
            return Letters[c] + n;
        }

        /// <summary>The face atlas code of a v4 colour and number ({c, n} of the okey, a represented face).</summary>
        public static string Code(string color, int number) => color + number;

        /// <summary>Int meld id of a v4 meld id ("m7" → 7); -1 when it is not one.</summary>
        public static int MeldInt(string meld) => meld != null && meld.Length > 1 && meld[0] == 'm' && int.TryParse(meld.Substring(1), out var n) ? n : -1;

        public static string MeldV4(int meld) => "m" + meld;
    }
}
