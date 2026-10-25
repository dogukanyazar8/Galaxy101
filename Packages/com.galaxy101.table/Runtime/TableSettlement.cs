using System;
using System.Collections.Generic;
using Okey101.Core.V4;

namespace Galaxy101.Table
{
    /// <summary>A match's money and points per seat (matchResult payouts / xpBonus / lp / weekly).</summary>
    public sealed class SettlementShares
    {
        public long[] Payouts = new long[4];
        public int[] XpBonus = new int[4];
        public int?[] Lp = new int?[4];
        public int[] Weekly = new int[4];
        /// <summary>Per seat, the place positions (0-based) its unit and the units tied with it occupy (D-40, D-41).</summary>
        public int[][] Occupied = new int[4][];
        /// <summary>Per seat, the place shown (1-based): the first position it occupies.</summary>
        public int Place(int seat) => Occupied[seat] == null || Occupied[seat].Length == 0 ? 4 : Occupied[seat][0] + 1;
    }

    /// <summary>
    /// mockServer.js matchOver arithmetic. Pool = the stakes paid (R-91, D-34), no rake; the units ranked 1st split it equally
    /// (D-12), a unit's share going to its people still at the table (R-60, R-91). Place values (XP bonus R-93, LP, weekly R-95)
    /// are shared: tied units — and in pairs a team, which occupies two positions (D-41) — take the average of the positions
    /// they occupy, rounded to nearest (D-40). Only people seated at the end get anything (leavers: none; D-31, D-37).
    /// </summary>
    public static class TableSettlement
    {
        /// <param name="ranking">The engine's ranking (G101Rules.Ranking: units with seats and rank).</param>
        /// <param name="person">Seats a person holds at the end.</param>
        /// <param name="pool">Stakes paid into the match (0: no chips, e.g. ranked or against bots).</param>
        /// <param name="lpTable">Ranked only: a seat's LP by position 1–4 (its league's table); null otherwise.</param>
        /// <param name="weekly">Weekly points by position, or null when the match does not count (private, against bots).</param>
        /// <param name="xpPlace">XP bonus by position (R-93).</param>
        public static SettlementShares Compute(IList<RankUnit> ranking, bool[] person, long pool, Func<int, int[]> lpTable, int[] weekly, int[] xpPlace)
        {
            var o = new SettlementShares();
            if (pool > 0)
            {
                var winners = new List<RankUnit>();
                foreach (var u in ranking) if (u.Rank == 1) winners.Add(u);
                var share = winners.Count == 0 ? 0 : (long)Math.Floor(pool * (1.0 - TableEconomy.Rake / 100.0) / winners.Count);
                foreach (var u in winners)
                {
                    var people = new List<int>();
                    foreach (var k in u.Seats) if (person[k]) people.Add(k);
                    foreach (var k in people) o.Payouts[k] += share / people.Count;
                }
            }
            var position = 0;
            var i = 0;
            while (i < ranking.Count)
            {
                var j = i;
                var n = 0;
                while (j < ranking.Count && ranking[j].Rank == ranking[i].Rank) { n += ranking[j].Seats.Length; j++; }
                var range = new int[n];
                for (var p = 0; p < n; p++) range[p] = position + p;
                for (var q = i; q < j; q++) foreach (var k in ranking[q].Seats) o.Occupied[k] = range;
                position += n;
                i = j;
            }
            for (var k = 0; k < 4; k++)
            {
                if (!person[k] || o.Occupied[k] == null) continue;
                o.XpBonus[k] = Shared(xpPlace, o.Occupied[k]);
                if (lpTable != null) o.Lp[k] = Shared(lpTable(k), o.Occupied[k]);
                if (weekly != null) o.Weekly[k] = Shared(weekly, o.Occupied[k]);
            }
            return o;
        }

        /// <summary>D-40: the average of the values at the occupied positions, rounded to nearest (JavaScript Math.round).</summary>
        public static int Shared(int[] values, int[] positions)
        {
            double total = 0;
            foreach (var p in positions) total += values[p];
            return TableEconomy.JsRound(total / positions.Length);
        }
    }
}
