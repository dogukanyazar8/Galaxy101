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
            var loose = new List<string>();
            foreach (var id in All) if (!Valid.Contains(id)) loose.Add(id);
            if (Groups.Count > 0 && loose.Count == 1 && Sum >= runNeed) return (Groups, loose[0]);
            var single = new List<string>();
            foreach (var id in All) if (!Paired.Contains(id)) single.Add(id);
            if (Pairs.Count >= pairNeed && single.Count == 1) return (Pairs, single[0]);
            return null;
        }
    }
}
