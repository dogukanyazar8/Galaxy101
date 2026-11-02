using System.Collections.Generic;
using Okey101.Core.V4;

namespace Galaxy101.Table
{
    /// <summary>
    /// What the person's rack proposes (prototype evalHand / segVal / pairsOf / engKind): every gap-free rack segment of
    /// three or more tiles the engine accepts as a run or a group (R-16: the client names the kind; the tiles go in rack
    /// order, so an okey stands where it lies, R-13), and every two-tile segment that is a pair. The engine's analysis
    /// decides validity and value (<see cref="G101Rules.AnalyzeMeld"/>); the opening itself is the engine's (gOpen).
    /// </summary>
    public sealed class RackMelds
    {
        public readonly List<MeldSpec> Groups = new List<MeldSpec>();
        public readonly List<MeldSpec> Pairs = new List<MeldSpec>();
        /// <summary>Tiles in a valid run or group (the green "geçerli per" bars).</summary>
        public readonly HashSet<string> Valid = new HashSet<string>();
        /// <summary>Tiles in a pair segment.</summary>
        public readonly HashSet<string> Paired = new HashSet<string>();
        public readonly List<string> All = new List<string>();
        /// <summary>Total of the runs and groups (the gauge's "88 / 101").</summary>
        public int Sum;

        /// <param name="segments">The rack's gap-free segments, each in rack order.</param>
        public static RackMelds Of(IEnumerable<IList<string>> segments, ColorNum okey)
        {
            var o = new RackMelds();
            foreach (var seg in segments)
            {
                o.All.AddRange(seg);
                if (okey == null) continue;
                if (seg.Count >= 3)
                {
                    var tiles = new List<string>(seg);
                    var run = G101Rules.AnalyzeMeld(okey, G101Rules.KindRun, tiles);
                    var group = G101Rules.AnalyzeMeld(okey, G101Rules.KindGroup, tiles);
                    if (run.Ok || group.Ok)
                    {
                        // Preserve explicit series/group segments, including their chosen joker interpretation.
                        bool asGroup = group.Ok && (!run.Ok || group.Value > run.Value);
                        o.Groups.Add(new MeldSpec(asGroup ? G101Rules.KindGroup : G101Rules.KindRun, tiles));
                        o.Sum += asGroup ? group.Value : run.Value;
                        foreach (var id in tiles) o.Valid.Add(id);
                        continue;
                    }
                }
                // A full rack has too few cells to separate every pair and the loose tiles. "Cift diz" keeps
                // its surplus pairs adjacent in the loose tail; they remain usable for opening and finishing.
                for (int i = 0; i + 1 < seg.Count; i++)
                {
                    var pair = new List<string> { seg[i], seg[i + 1] };
                    if (!G101Rules.AnalyzeMeld(okey, G101Rules.KindPair, pair).Ok) continue;
                    o.Pairs.Add(new MeldSpec(G101Rules.KindPair, pair));
                    foreach (var id in pair) o.Paired.Add(id);
                    i++;
                }
            }
            return o;
        }

        /// <summary>
        /// Elden bitiş (prototype bombInfo, R-53): every tile but one in runs/groups reaching the run bar, or the pairs
        /// reaching the pair bar with one tile left. The melds to open and the tile to finish with; null when neither.
        /// </summary>
        public (List<MeldSpec> Melds, string Last)? FromHand(int runNeed, int pairNeed)
        {
            var plan = FromHand(runNeed, pairNeed, null, null);
            return plan.HasValue ? (plan.Value.Melds, plan.Value.Last) : ((List<MeldSpec>, string)?)null;
        }

        /// <summary>
        /// Elden bitiş with işleme (R-53 + R-25): the hand opens, the loose tiles are laid onto the table's melds and the
        /// opening's own runs/groups, one tile is left to finish with. <paramref name="table"/> null: no laying off.
        /// <c>Lays</c> lists the tiles to lay, in an order the engine accepts one by one.
        /// </summary>
        public (List<MeldSpec> Melds, string Last, List<string> Lays)? FromHand(int runNeed, int pairNeed, IEnumerable<(string Kind, List<string> Tiles)> table, ColorNum okey)
        {
            var loose = new List<string>();
            foreach (var id in All) if (!Valid.Contains(id)) loose.Add(id);
            if (Groups.Count > 0 && Sum >= runNeed && Lay(Groups, loose, table, okey) is var runs && runs.HasValue) return (Groups, runs.Value.Last, runs.Value.Lays);
            var single = new List<string>();
            foreach (var id in All) if (!Paired.Contains(id)) single.Add(id);
            if (Pairs.Count >= pairNeed && Pairs.Count > 0 && Lay(Pairs, single, table, okey) is var pairs && pairs.HasValue) return (Pairs, pairs.Value.Last, pairs.Value.Lays);
            return null;
        }

        private static (string Last, List<string> Lays)? Lay(List<MeldSpec> opening, List<string> loose, IEnumerable<(string Kind, List<string> Tiles)> table, ColorNum okey)
        {
            if (loose.Count == 1) return (loose[0], new List<string>());
            if (loose.Count == 0 || table == null || okey == null) return null;
            var melds = new List<(string Kind, List<string> Tiles)>();
            foreach (var m in table) melds.Add((m.Kind, new List<string>(m.Tiles)));
            foreach (var m in opening) melds.Add((m.Kind, new List<string>(m.Tiles)));
            var left = new List<string>(loose); var lays = new List<string>();
            for (bool progress = true; progress && left.Count > 0;)
            {
                progress = false;
                for (int i = 0; i < left.Count && !progress; i++)
                    foreach (var m in melds)
                    {
                        var sides = G101Rules.AddSides(okey, m.Kind, m.Tiles, left[i]);
                        if (sides.Count == 0) continue;
                        if (sides[0] == "pre") m.Tiles.Insert(0, left[i]); else m.Tiles.Add(left[i]);
                        lays.Add(left[i]); left.RemoveAt(i); progress = true; break;
                    }
            }
            // Every loose tile fits: the last one laid (nothing was laid after it) is the one to finish with.
            if (left.Count == 0) { string last = lays[lays.Count - 1]; lays.RemoveAt(lays.Count - 1); return (last, lays); }
            return left.Count == 1 ? (left[0], lays) : ((string, List<string>)?)null;
        }
    }
}
