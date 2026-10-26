using System.Collections.Generic;
using System.Text;

namespace Okey101.Core.V4
{
    /// <summary>
    /// Line-by-line port of the handoff v4 bot module (<c>logic/bots.js</c> v0.4.0; D-20 timing, D-21 policy, R-80).
    /// A bot sees only its own rack and public information (table melds, discard piles, indicator) — never the deck order or
    /// other racks. Game decisions are deterministic; randomness is only for chat and comes from a seeded <see cref="RngState"/>.
    /// Lives in the core package so the server (Galaxy101.Rules) compiles it with the engine.
    /// </summary>
    public static class G101Bots
    {
        /// <summary>D-20 thinking times (ms): turn start → draw 600, each meld/add 430, discard 650 after a meld / 730 directly.</summary>
        public static class Timing
        {
            public const int Draw = 600, Meld = 430, DiscardAfterMeld = 650, Discard = 730, TurnStartDelay = 120;
        }

        /// <summary>D-20 chat: after each discard 16 % one of bot.1–bot.6; a player's message is answered 60 % after 1400 ms; a gift is thanked after 1900 ms (rocket 6300 ms).</summary>
        public static class Chat
        {
            public const double AfterDiscard = 0.16;
            public static readonly IReadOnlyList<string> Lines = new[] { "bot.1", "bot.2", "bot.3", "bot.4", "bot.5", "bot.6" };
            public const double ReplyChance = 0.6;
            public const int ReplyDelay = 1400, ThanksDelay = 1900, ThanksDelayRocket = 6300;
        }

        private sealed class Shape
        {
            public string Kind;
            public int[] Keys;
            public int Value;
        }

        private static List<Shape>[] shapesAt;
        private static readonly object ShapesLock = new object();

        private static List<Shape>[] Shapes()
        {
            lock (ShapesLock)
            {
                if (shapesAt != null) return shapesAt;
                var at = new List<Shape>[52];
                int c, s, l, n, j;
                for (j = 0; j < 52; j++) at[j] = new List<Shape>();
                void Add(Shape sh)
                {
                    var seen = new HashSet<int>();
                    foreach (var q in sh.Keys) if (seen.Add(q)) at[q].Add(sh);
                    var v = 0;
                    foreach (var q in sh.Keys) v += (q % 13) + 1;
                    sh.Value = v;
                }
                for (c = 0; c < 4; c++)
                    for (s = 1; s <= 11; s++)
                        for (l = 3; s + l - 1 <= 13; l++)
                        {
                            var k1 = new int[l];
                            for (j = 0; j < l; j++) k1[j] = c * 13 + s - 1 + j;
                            Add(new Shape { Kind = G101Rules.KindRun, Keys = k1 });
                        }
                for (n = 0; n < 13; n++)
                {
                    var all = new[] { 0 * 13 + n, 1 * 13 + n, 2 * 13 + n, 3 * 13 + n };
                    Add(new Shape { Kind = G101Rules.KindGroup, Keys = all });
                    for (var skip = 0; skip < 4; skip++)
                    {
                        var g = new List<int>();
                        for (var i = 0; i < 4; i++) if (i != skip) g.Add(all[i]);
                        Add(new Shape { Kind = G101Rules.KindGroup, Keys = g.ToArray() });
                    }
                }
                shapesAt = at;
                return at;
            }
        }

        private static int KeyOf(ColorNum ok, string id)
        {
            var r = G101Rules.RealOf(ok, id);
            var ci = 0;
            for (var i = 0; i < G101Rules.Colors.Count; i++) if (G101Rules.Colors[i] == r.C) { ci = i; break; }
            return ci * 13 + r.N - 1;
        }

        public sealed class BestMeldsResult
        {
            public int Value;
            public List<MeldSpec> Melds;
            public List<string> Left;
        }

        private sealed class DfsResult
        {
            public static readonly DfsResult Empty = new DfsResult { V = 0, M = new List<(Shape sh, List<int> jq)>() };
            public int V;
            public List<(Shape sh, List<int> jq)> M;
        }

        /// <summary>The highest-total set of runs/groups; okeys fill the gaps.</summary>
        public static BestMeldsResult BestMelds(ColorNum ok, IList<string> ids)
        {
            var at = Shapes();
            var cnt = new int[52];
            var pool = new List<string>[52];
            var memo = new Dictionary<string, DfsResult>();
            int i;
            for (i = 0; i < 52; i++) pool[i] = new List<string>();
            var jok = new List<string>();
            var rest = new List<string>();
            foreach (var id in ids) { if (G101Rules.IsOkey(ok, id)) jok.Add(id); else rest.Add(id); }
            var sorted = new List<string>(rest);
            sorted.Sort(string.CompareOrdinal);
            foreach (var id in sorted) { var k = KeyOf(ok, id); cnt[k]++; pool[k].Add(id); }
            var sb = new StringBuilder();
            DfsResult Dfs(int p, int jl)
            {
                while (p < 52 && cnt[p] == 0) p++;
                if (p >= 52) return DfsResult.Empty;
                sb.Clear();
                sb.Append(p).Append('|').Append(jl).Append('|');
                for (var x = p; x < 52; x++) sb.Append(cnt[x]);
                var mk = sb.ToString();
                if (memo.TryGetValue(mk, out var hit)) return hit;
                cnt[p]--;
                var best = Dfs(p, jl);
                cnt[p]++;
                var list = at[p];
                for (var a = 0; a < list.Count; a++)
                {
                    var sh = list[a];
                    var real = new List<int>();
                    var jq = new List<int>();
                    var seenP = false;
                    for (var b = 0; b < sh.Keys.Length; b++)
                    {
                        var q = sh.Keys[b];
                        if (q == p && !seenP) { seenP = true; real.Add(q); }
                        else if (q > p && cnt[q] > 0) real.Add(q);
                        else jq.Add(b);
                    }
                    if (jq.Count > jl) continue;
                    foreach (var q in real) cnt[q]--;
                    var r = Dfs(p, jl - jq.Count);
                    var v = r.V + sh.Value;
                    foreach (var q in real) cnt[q]++;
                    if (v > best.V)
                    {
                        var m = new List<(Shape sh, List<int> jq)>(r.M.Count + 1) { (sh, jq) };
                        m.AddRange(r.M);
                        best = new DfsResult { V = v, M = m };
                    }
                }
                memo[mk] = best;
                return best;
            }
            var res = Dfs(0, jok.Count);
            var js = new List<string>(jok);
            var pl = new List<string>[52];
            for (i = 0; i < 52; i++) pl[i] = new List<string>(pool[i]);
            var melds = new List<MeldSpec>();
            foreach (var o in res.M)
            {
                var tiles = new List<string>();
                for (var b = 0; b < o.sh.Keys.Length; b++)
                {
                    var q = o.sh.Keys[b];
                    if (o.jq.Contains(b)) { tiles.Add(js[0]); js.RemoveAt(0); }
                    else { tiles.Add(pl[q][0]); pl[q].RemoveAt(0); }
                }
                melds.Add(new MeldSpec(o.sh.Kind, tiles));
            }
            var left = new List<string>();
            foreach (var x in pl) left.AddRange(x);
            left.AddRange(js);
            return new BestMeldsResult { Value = res.V, Melds = melds, Left = left };
        }

        public sealed class BestPairsResult
        {
            public List<MeldSpec> Pairs;
            public List<string> Left;
        }

        /// <summary>Pairs: identical tiles; an okey pairs with the highest single, two okeys pair with each other.</summary>
        public static BestPairsResult BestPairs(ColorNum ok, IList<string> ids)
        {
            var by = new Dictionary<string, List<string>>();
            var order = new List<string>();
            var pairs = new List<MeldSpec>();
            var singles = new List<string>();
            var js = new List<string>();
            var sorted = new List<string>(ids);
            sorted.Sort(string.CompareOrdinal);
            foreach (var id in sorted)
            {
                if (G101Rules.IsOkey(ok, id)) { js.Add(id); continue; }
                var r = G101Rules.RealOf(ok, id);
                var k = r.C + r.N;
                if (!by.ContainsKey(k)) { by[k] = new List<string>(); order.Add(k); }
                by[k].Add(id);
            }
            foreach (var k in order)
            {
                var a = by[k];
                while (a.Count >= 2) { pairs.Add(new MeldSpec(G101Rules.KindPair, new List<string> { a[0], a[1] })); a.RemoveRange(0, 2); }
                if (a.Count > 0) { singles.Add(a[0]); a.RemoveAt(0); }
            }
            G101Rules.StableSort(singles, (a, b) =>
            {
                var d = G101Rules.RealOf(ok, b).N - G101Rules.RealOf(ok, a).N;
                return d != 0 ? d : string.CompareOrdinal(a, b) < 0 ? -1 : 1;
            });
            while (js.Count > 0 && singles.Count > 0) { pairs.Add(new MeldSpec(G101Rules.KindPair, new List<string> { singles[0], js[0] })); singles.RemoveAt(0); js.RemoveAt(0); }
            if (js.Count == 2) { pairs.Add(new MeldSpec(G101Rules.KindPair, new List<string> { js[0], js[1] })); js.RemoveRange(0, 2); }
            var left = new List<string>(singles);
            left.AddRange(js);
            return new BestPairsResult { Pairs = pairs, Left = left };
        }

        private static int SumMelds(ColorNum ok, List<MeldSpec> melds)
        {
            var s = 0;
            foreach (var m in melds) s += G101Rules.AnalyzeMeld(ok, m.Kind, m.Tiles).Value;
            return s;
        }
        private static bool Contains(List<MeldSpec> melds, string id)
        {
            foreach (var m in melds) if (m.Tiles.Contains(id)) return true;
            return false;
        }

        // R-35 at least one tile stays on the rack: when none would, drop the smallest meld or one end of a 4+ meld.
        private static List<MeldSpec> KeepOne(ColorNum ok, List<MeldSpec> melds, int? min, string must)
        {
            var opts = new List<List<MeldSpec>>();
            int Score(List<MeldSpec> ms) => ms.Count > 0 && ms[0].Kind == G101Rules.KindPair ? ms.Count : SumMelds(ok, ms);
            for (var i = 0; i < melds.Count; i++)
            {
                if (must != null && melds[i].Tiles.Contains(must)) continue;
                var rest = new List<MeldSpec>();
                for (var j = 0; j < melds.Count; j++) if (j != i) rest.Add(melds[j]);
                if (rest.Count > 0 && (min == null || Score(rest) >= min.Value)) opts.Add(rest);
                var m = melds[i];
                if (m.Tiles.Count >= 4)
                {
                    var variants = new[] { m.Tiles.GetRange(1, m.Tiles.Count - 1), m.Tiles.GetRange(0, m.Tiles.Count - 1) };
                    foreach (var t2 in variants)
                    {
                        if (must != null && m.Tiles.Contains(must) && !t2.Contains(must)) continue;
                        if (!G101Rules.AnalyzeMeld(ok, m.Kind, t2).Ok) continue;
                        var alt = new List<MeldSpec>();
                        for (var j = 0; j < melds.Count; j++) alt.Add(j == i ? new MeldSpec(m.Kind, t2) : melds[j]);
                        if (min == null || Score(alt) >= min.Value) opts.Add(alt);
                    }
                }
            }
            G101Rules.StableSort(opts, (a, b) => Score(b) - Score(a));
            return opts.Count > 0 ? opts[0] : null;
        }

        private static List<MeldSpec> RunPlan(ColorNum ok, List<string> rack, string must, int need)
        {
            var b = BestMelds(ok, rack);
            if (b.Melds.Count == 0) return null;
            var ms = b.Melds;
            if (b.Left.Count == 0) ms = KeepOne(ok, ms, need, must);
            if (ms == null || SumMelds(ok, ms) < need) return null;
            if (must != null && !Contains(ms, must)) return null;
            return ms;
        }
        private static List<MeldSpec> PairPlan(ColorNum ok, List<string> rack, string must, int need)
        {
            var b = BestPairs(ok, rack);
            if (b.Pairs.Count < need) return null;
            var ms = b.Pairs;
            if (b.Left.Count == 0)
            {
                var di = -1;
                for (var i = ms.Count - 1; i >= 0; i--) if (must == null || !ms[i].Tiles.Contains(must)) { di = i; break; }
                if (di < 0) return null;
                var filtered = new List<MeldSpec>();
                for (var j = 0; j < ms.Count; j++) if (j != di) filtered.Add(ms[j]);
                ms = filtered;
            }
            if (ms.Count < need) return null;
            if (must != null && !Contains(ms, must)) return null;
            return ms;
        }
        private static int Left(List<string> rack, List<MeldSpec> ms)
        {
            var n = rack.Count;
            foreach (var m in ms) n -= m.Tiles.Count;
            return n;
        }

        /// <summary>The opening the bot would make (with an extra tile, e.g. the side tile, when given); must = a tile the opening has to use.</summary>
        public static List<MeldSpec> OpeningPlan(MatchState s, int seat, string extra, string must)
        {
            var h = s.Hand;
            var ok = h.Okey;
            var rack = new List<string>(h.Racks[seat]);
            if (extra != null) rack.Add(extra);
            var rp = RunPlan(ok, rack, must, G101Rules.OpenNeed(s, seat, G101Rules.KindRun));
            var pp = PairPlan(ok, rack, must, G101Rules.OpenNeed(s, seat, G101Rules.KindPair));
            if (pp != null && Left(rack, pp) <= 1 && !(rp != null && Left(rack, rp) <= 1)) return pp;          // finishing from the hand with pairs
            return rp ?? pp;
        }

        /// <summary>Whether the bot takes the left player's discard t.</summary>
        public static bool WantSide(MatchState s, int seat, string t)
        {
            var h = s.Hand;
            var ok = h.Okey;
            var rack = h.Racks[seat];
            if (h.Opened[seat] == 0) return OpeningPlan(s, seat, t, t) != null;
            if (h.OpenMode[seat] == G101Rules.KindPair)
            {
                foreach (var id in rack) if (G101Rules.AnalyzeMeld(ok, G101Rules.KindPair, new List<string> { id, t }).Ok) return true;
                return false;
            }
            var a = BestMelds(ok, rack);
            var withT = new List<string>(rack) { t };
            var b = BestMelds(ok, withT);
            if (b.Value > a.Value && Contains(b.Melds, t)) return true;
            return s.Opts.Isle && G101Rules.IsIslek(s, t);
        }

        /// <summary>The bot's next single action. The server applies it, waits <see cref="DelayFor"/>, and asks again.</summary>
        public static GameAction NextAction(MatchState s, int seat)
        {
            var h = s.Hand;
            var t = h.Turn;
            var ok = h.Okey;
            var rack = h.Racks[seat];
            if (t.Phase == G101Rules.PhaseDraw)
            {
                var pile = h.Discards[G101Rules.LeftOf(seat)];
                if (!t.SideReturned && pile.Count > 0 && WantSide(s, seat, pile[pile.Count - 1])) return GameAction.Draw(seat, "side");
                return GameAction.Draw(seat, "deck");
            }
            if (h.Opened[seat] == 0)
            {
                var plan = OpeningPlan(s, seat, null, t.SideMust ? t.SideTake : null);
                if (plan != null) return GameAction.Open(seat, plan);
                if (t.SideMust) return GameAction.ReturnSide(seat);
                return GameAction.Discard(seat, G101Rules.AutoDiscardTile(s, seat));
            }
            List<MeldSpec> lay = null;
            if (h.OpenMode[seat] == G101Rules.KindPair)
            {
                var bp = BestPairs(ok, rack);
                if (bp.Pairs.Count > 0) { lay = bp.Pairs; if (bp.Left.Count == 0) lay = lay.GetRange(0, lay.Count - 1); }
            }
            else
            {
                var bm = BestMelds(ok, rack);
                if (bm.Melds.Count > 0) { lay = bm.Melds; if (bm.Left.Count == 0) lay = KeepOne(ok, lay, null, null); }
            }
            if (lay != null && lay.Count > 0 && !OnlySideLeft(rack, t.SideTake, Used(lay))) return GameAction.Lay(seat, lay);
            if (s.Opts.Isle)
            {
                int i, m;
                var ids = new List<string>(rack);
                ids.Sort(string.CompareOrdinal);
                if (rack.Count >= 2)
                    for (i = 0; i < ids.Count; i++)
                    {
                        if (G101Rules.IsOkey(ok, ids[i]) && rack.Count > 2) continue;                              // add the okey only when about to finish
                        if (OnlySideLeft(rack, t.SideTake, new HashSet<string> { ids[i] })) continue;
                        for (m = 0; m < h.Melds.Count; m++)
                        {
                            var sides = G101Rules.AddSides(s, h.Melds[m].Id, ids[i]);
                            if (sides.Count > 0) return GameAction.Add(seat, ids[i], h.Melds[m].Id, sides[0]);
                        }
                    }
                for (m = 0; m < h.Melds.Count; m++)
                {
                    var g = h.Melds[m];
                    for (var x = 0; x < g.Tiles.Count; x++)
                    {
                        if (!G101Rules.IsOkey(ok, g.Tiles[x])) continue;
                        if (G101Rules.PartnerOf(s, seat) == g.PlacedBy[x]) continue;                                // never take the partner's okey
                        for (i = 0; i < ids.Count; i++) if (G101Rules.SwapFits(s, g.Id, x, ids[i])) return GameAction.Swap(seat, ids[i], g.Id, x);
                    }
                }
            }
            var discard = G101Rules.AutoDiscardTile(s, seat);
            if (discard == null && t.SideTake != null && rack.Contains(t.SideTake)) return GameAction.ReturnSide(seat);   // R-32: the side tile alone cannot be discarded
            return GameAction.Discard(seat, discard);
        }

        private static HashSet<string> Used(List<MeldSpec> melds)
        {
            var used = new HashSet<string>();
            foreach (var m in melds) foreach (var id in m.Tiles) used.Add(id);
            return used;
        }
        // Would playing <paramref name="used"/> leave only the side tile, which may not be discarded (R-32)?
        private static bool OnlySideLeft(List<string> rack, string side, HashSet<string> used)
        {
            if (side == null || used.Contains(side) || !rack.Contains(side)) return false;
            foreach (var id in rack) if (id != side && !used.Contains(id)) return false;
            return true;
        }

        /// <summary>D-20 delay before <paramref name="next"/> after <paramref name="prev"/> (null at turn start).</summary>
        public static int DelayFor(GameAction prev, GameAction next)
        {
            if (next.T == "draw") return Timing.Draw;
            if (next.T == "discard") return prev != null && prev.T != "draw" ? Timing.DiscardAfterMeld : Timing.Discard;
            return Timing.Meld;
        }

        /// <summary>D-20 chat after a discard: 16 % a line key (bot.1…bot.6), otherwise null. Uses the given seeded stream.</summary>
        public static string ChatAfterDiscard(RngState rng)
        {
            if (G101Rules.RngNext(rng) / 4294967296.0 >= Chat.AfterDiscard) return null;
            return Chat.Lines[G101Rules.RngInt(rng, Chat.Lines.Count)];
        }

        public sealed class SimLogEntry
        {
            public string A;
            public int Seat;
            public List<string> Ev;
            public string Fail;
            public GameAction Action;
            public string Fatal;
        }

        public sealed class SimResult
        {
            public MatchState State;
            public List<SimLogEntry> Log;
        }

        /// <summary>A whole match with four bots (tests and balance). Rejected bot actions are logged and replaced by a timeout.</summary>
        public static SimResult Simulate(MatchOptions opts, uint seed, int maxSteps = 0)
        {
            var s = G101Rules.NewMatch(opts, seed);
            var log = new List<SimLogEntry>();
            var r = G101Rules.Apply(s, GameAction.StartHand());
            s = r.State;
            var guard = maxSteps != 0 ? maxSteps : 20000;
            while (guard-- > 0)
            {
                if (s.Status == G101Rules.StatusMatchOver) break;
                if (s.Status == G101Rules.StatusHandOver) { s = G101Rules.Apply(s, GameAction.NextHand()).State; continue; }
                var seat = s.Hand.Turn.Seat;
                var a = NextAction(s, seat);
                var res = G101Rules.Apply(s, a);
                if (!res.Ok)
                {
                    log.Add(new SimLogEntry { Fail = res.Error, Action = a, Seat = seat });
                    res = G101Rules.Apply(s, GameAction.Timeout(seat));
                    if (!res.Ok) { log.Add(new SimLogEntry { Fatal = res.Error, Seat = seat }); break; }
                }
                var names = new List<string>();
                foreach (var e in res.Events) names.Add(e.E);
                log.Add(new SimLogEntry { A = a.T, Seat = seat, Ev = names });
                s = res.State;
            }
            return new SimResult { State = s, Log = log };
        }
    }
}
