using System;
using System.Collections.Generic;

namespace Okey101.Core.V4
{
    /// <summary>
    /// Line-by-line port of the handoff v4 rules engine (<c>logic/rules.js</c> v0.4.0, RULES.md R-01…R-80, DECISIONS D-01…D-21).
    /// Pure: <see cref="Apply"/> never changes its input and returns a new state plus the events. Randomness only from the
    /// state's seeded mulberry32 stream (D-02). String tile ids (D-01): colour + number + copy ('R5a', 'R5b'), fake okeys 'Ja', 'Jb'.
    /// Rule ids in comments match RULES.md; the order of statements follows rules.js so the two can be read side by side.
    /// </summary>
    public static class G101Rules
    {
        public const string Version = "0.4.0";
        /// <summary>Record versions of matches dealt and played by this engine (K-02).</summary>
        public const string RuleVersion = "101-v4";
        public const string ShuffleVersion = "mulberry32-v4";

        public static readonly IReadOnlyList<string> Colors = new[] { "R", "B", "Y", "K" };    // red, blue, yellow, black

        /// <summary>rules.js C.</summary>
        public static class Const
        {
            public const int OpenRunMin = 101;     // R-20
            public const int OpenPairMin = 5;      // R-21
            public const int Penalty = 101;        // R-40..R-44
            public const int OpenedWithX = 10;     // R-43
            public const int NotOpened = 202;      // R-51
            public const int Finish = -101;        // R-50
            public const int OkeyInHand = 101;     // R-52
            public const int Stacks = 15, StackH = 7, Deck = 20;
        }

        public const string StatusLobby = "lobby", StatusPlaying = "playing", StatusHandOver = "handOver", StatusMatchOver = "matchOver";
        public const string PhaseDraw = "draw", PhasePlay = "play", PhaseOver = "over";
        public const string KindRun = "run", KindGroup = "group", KindPair = "pair";
        public const string PenOpenShort = "P_OPEN_SHORT", PenIslek = "P_ISLEK", PenOkeyDiscard = "P_OKEY_DISCARD", PenOpenedWith = "P_OPENED_WITH", PenOkeyTaken = "P_OKEY_TAKEN";

        // ---------- RNG (mulberry32, uint32) ----------
        public static uint RngNext(RngState r)
        {
            unchecked
            {
                r.S = r.S + 0x6D2B79F5u;
                var t = r.S;
                t = (t ^ (t >> 15)) * (t | 1u);
                t ^= t + (t ^ (t >> 7)) * (t | 61u);
                return t ^ (t >> 14);
            }
        }
        public static int RngInt(RngState r, int n) => (int)(RngNext(r) % (uint)n);

        // ---------- Tiles (R-01..R-05) ----------
        // id: colour + number + copy ('R5a', 'R5b'); fake okeys 'Ja', 'Jb'. Every id exists exactly once → the 2-copy limit is structural.
        public static readonly IReadOnlyList<string> AllTiles = BuildAllTiles();
        private static readonly HashSet<string> AllSet = new HashSet<string>(AllTiles);
        private static readonly Dictionary<string, int> Numbers = BuildNumbers();

        private static string[] BuildAllTiles()
        {
            var a = new List<string>();
            for (var ci = 0; ci < 4; ci++) for (var n = 1; n <= 13; n++) { a.Add(Colors[ci] + n + "a"); a.Add(Colors[ci] + n + "b"); }
            a.Add("Ja"); a.Add("Jb");
            return a.ToArray();
        }
        private static Dictionary<string, int> BuildNumbers()
        {
            var d = new Dictionary<string, int>();
            foreach (var id in AllTiles) d[id] = ParseNumber(id);
            return d;
        }
        private static int ParseNumber(string id)
        {
            if (IsFake(id)) return 0;
            return id.Length > 2 && int.TryParse(id.Substring(1, id.Length - 2), out var n) ? n : 0;
        }

        public static bool IsTile(string id) => id != null && AllSet.Contains(id);
        public static bool IsFake(string id) => !string.IsNullOrEmpty(id) && id[0] == 'J';
        public static string ColorOf(string id) => string.IsNullOrEmpty(id) ? "" : id.Substring(0, 1);
        public static int NumOf(string id) => id != null && Numbers.TryGetValue(id, out var n) ? n : ParseNumber(id ?? "");
        public static string CodeOf(string id) => IsFake(id) ? "J" : id.Substring(0, id.Length - 1);
        public static ColorNum OkeyFromIndicator(string ind) => new ColorNum(ColorOf(ind), (NumOf(ind) % 13) + 1);
        public static bool IsOkey(ColorNum ok, string id) => id != null && !IsFake(id) && ColorOf(id) == ok.C && NumOf(id) == ok.N;
        public static ColorNum RealOf(ColorNum ok, string id) => IsFake(id) ? new ColorNum(ok.C, ok.N) : new ColorNum(ColorOf(id), NumOf(id));
        public static int HandValueOf(ColorNum ok, string id) => IsOkey(ok, id) ? Const.OkeyInHand : RealOf(ok, id).N;   // R-52

        // ---------- Meld analysis (R-10..R-16) ----------
        // kind: 'run' (seri) | 'group' (grup) | 'pair' (çift). tiles in order; an okey's value comes from its position.
        public static MeldAnalysis AnalyzeMeld(ColorNum ok, string kind, IList<string> tiles)
        {
            var bad = MeldAnalysis.Bad;
            if (tiles == null || tiles.Count == 0) return bad;
            var l = tiles.Count;
            var reals = new List<(int i, string c, int n)>();
            int i;
            for (i = 0; i < l; i++) if (!IsOkey(ok, tiles[i])) { var r = RealOf(ok, tiles[i]); reals.Add((i, r.C, r.N)); }
            if (kind == KindPair)
            {
                if (l != 2) return bad;
                if (reals.Count == 2 && (reals[0].c != reals[1].c || reals[0].n != reals[1].n)) return bad;
                var pr = reals.Count > 0 ? new ColorNum(reals[0].c, reals[0].n) : new ColorNum(ok.C, ok.N);
                return new MeldAnalysis { Ok = true, Kind = KindPair, Value = 0, Repr = new List<ColorNum> { pr, pr } };
            }
            if (reals.Count == 0) return bad;                                                        // R-14
            if (kind == KindRun)
            {
                if (l < 3 || l > 13) return bad;
                var r0 = reals[0];
                var st = r0.n - r0.i;                                                                // R-12 no wrap: 1 never follows 13
                if (st < 1 || st + l - 1 > 13) return bad;
                for (i = 0; i < reals.Count; i++) if (reals[i].c != r0.c || reals[i].n != st + reals[i].i) return bad;
                var repr = new List<ColorNum>();
                var val = 0;
                for (i = 0; i < l; i++) { repr.Add(new ColorNum(r0.c, st + i)); val += st + i; }
                return new MeldAnalysis { Ok = true, Kind = KindRun, Value = val, C = r0.c, Start = st, End = st + l - 1, Repr = repr };
            }
            if (kind == KindGroup)
            {
                if (l < 3 || l > 4) return bad;
                var seen = new HashSet<string>();
                for (i = 0; i < reals.Count; i++) { if (reals[i].n != reals[0].n || seen.Contains(reals[i].c)) return bad; seen.Add(reals[i].c); }
                var miss = new List<string>();
                foreach (var c in Colors) if (!seen.Contains(c)) miss.Add(c);
                var repr2 = new List<ColorNum>();
                var mi = 0;
                for (i = 0; i < l; i++) repr2.Add(IsOkey(ok, tiles[i]) ? new ColorNum(miss[mi++], reals[0].n) : new ColorNum(RealOf(ok, tiles[i]).C, reals[0].n));
                return new MeldAnalysis { Ok = true, Kind = KindGroup, Value = reals[0].n * l, N = reals[0].n, Repr = repr2 };
            }
            return bad;
        }

        // ---------- Match ----------
        /// <summary>rules.js newMatch(opts, seed). Options are normalised as rules.js does (mode 'pairs' or 'solo', hands 0 → 5).</summary>
        public static MatchState NewMatch(MatchOptions opts, uint seed)
        {
            var o = opts ?? new MatchOptions();
            return new MatchState
            {
                V = Version,
                Opts = new MatchOptions { Mode = o.Mode == "pairs" ? "pairs" : "solo", Hands = o.Hands != 0 ? o.Hands : 5, Penalties = o.Penalties, Isle = o.Isle, Katlama = o.Katlama, KatlamaPartner = o.KatlamaPartner },
                Seed = seed, Rng = new RngState(seed),
                Status = StatusLobby, HandNo = 0, Dealer = -1,
                Totals = new int[4], History = new List<HistoryEntry>(),
                Seats = new[] { new SeatInfo(), new SeatInfo(), new SeatInfo(), new SeatInfo() },
                LeaveSeq = 0, Hand = null, Ranking = null
            };
        }

        private static HandState BlankHand() => new HandState();
        private static TurnState NewTurn(int seat, string phase, int n) => new TurnState { Seat = seat, Phase = phase, N = n, SideTake = null, SideMust = false, SideReturned = false, Snap = null, Pending = new List<PenaltyEntry>(), Drawn = null };

        // R-02..R-05 deal ceremony. rig: tests only {order, dealer, dice}.
        private static HandState DealHand(MatchState s, DealRig rig)
        {
            var h = BlankHand();
            var d = rig != null && rig.Dealer.HasValue ? rig.Dealer.Value : (s.HandNo == 1 ? RngInt(s.Rng, 4) : (s.Dealer + 1) % 4);
            List<string> ids;
            if (rig != null && rig.Order != null) ids = new List<string>(rig.Order);
            else
            {
                ids = new List<string>(AllTiles);
                for (var i = ids.Count - 1; i > 0; i--) { var j = RngInt(s.Rng, i + 1); var t = ids[i]; ids[i] = ids[j]; ids[j] = t; }
            }
            var stacks = new List<List<string>>();
            int si, k;
            for (si = 0; si < Const.Stacks; si++) stacks.Add(ids.GetRange(si * 7, 7));                 // lv 0 = bottom, 6 = top
            var initialStacks = V4Tree.CloneLists(stacks);
            var extra = ids[105];
            var a = rig != null && rig.Dice != null ? rig.Dice[0] : 1 + RngInt(s.Rng, 6);
            var b = rig != null && rig.Dice != null ? rig.Dice[1] : 1 + RngInt(s.Rng, 6);
            int ind = a - 1, lv = b - 1, lvF = lv;
            var st = stacks[ind];
            while (lvF < 7 && IsFake(st[lvF])) lvF++;                                                 // R-04 a fake okey is never the indicator: the one above
            if (lvF == 7) { lvF = lv - 1; while (lvF >= 0 && IsFake(st[lvF])) lvF--; }                 // none above → the one below (D-03)
            var indicator = st[lvF];
            st.RemoveAt(lvF);
            var s1 = (ind + 1) % 15;
            stacks[s1].Add(extra);                                                                     // R-03 extra tile
            int S_(int x) => (ind + x) % 15;
            var plan = new List<DealPlanStep>();
            for (k = 0; k < 4; k++) plan.Add(new DealPlanStep { Seat = (d + 1 + k) % 4, Stacks = new List<int> { S_(1 + k), S_(5 + k) } });
            for (k = 0; k < 4; k++) plan.Add(new DealPlanStep { Seat = (d + 1 + k) % 4, Stacks = new List<int> { S_(9 + k) } });
            foreach (var p in plan)
                foreach (var x in p.Stacks)
                {
                    var top = new List<string>(stacks[x]);
                    top.Reverse();
                    h.Racks[p.Seat].AddRange(top);
                }
            int c13 = S_(13), c14 = S_(14), right = Math.Max(c13, c14), left = Math.Min(c13, c14);
            h.Deck = new List<string>(stacks[ind]);                                                   // R-05 deck: H.deck[0] is drawn first
            h.Deck.AddRange(stacks[right]);
            h.Deck.AddRange(stacks[left]);
            h.Indicator = indicator;
            h.Okey = OkeyFromIndicator(indicator);
            h.Deal = new DealRecord
            {
                Dealer = d, Order = ids, Stacks = initialStacks, Extra = extra, Dice = new[] { a, b }, IndStack = ind, IndLevel = lvF, RolledLevel = lv,
                FakeSkip = lvF != lv, ExtraStack = s1, Plan = plan, CenterStacks = new[] { ind, right, left }
            };
            var starter = (d + 1) % 4;
            h.Turn = NewTurn(starter, PhasePlay, 1);                                                   // R-06 the 22-tile player discards without drawing
            s.Dealer = d;
            return h;
        }

        /// <summary>
        /// Scenario: builds the inverse of the deal from the wanted racks → {dealer, dice, order}. The deal ceremony stays real
        /// (same stacks, same dice). Short racks are filled from the remaining tiles; deckTop are the first tiles drawn.
        /// Throws <see cref="SpecException"/> (E_SPEC_TILE, E_SPEC_DUP, E_SPEC_IND, E_SPEC_RACK, E_SPEC_COUNT).
        /// </summary>
        public static DealRig RigFromHands(ScenarioDeal sc)
        {
            var d = sc.Dealer ?? 3;
            int a = sc.Dice != null ? sc.Dice[0] : 1, b = sc.Dice != null ? sc.Dice[1] : 1, ind = a - 1, lv = b - 1;
            var used = new HashSet<string>();
            string Take(string id)
            {
                if (!IsTile(id)) throw new SpecException("E_SPEC_TILE " + id);
                if (used.Contains(id)) throw new SpecException("E_SPEC_DUP " + id);
                used.Add(id);
                return id;
            }
            if (sc.Indicator == null || IsFake(sc.Indicator)) throw new SpecException("E_SPEC_IND");
            Take(sc.Indicator);
            var racks = new List<List<string>>();
            for (var k = 0; k < 4; k++)
            {
                var r = new List<string>();
                if (sc.Racks != null && k < sc.Racks.Count && sc.Racks[k] != null) foreach (var id in sc.Racks[k]) r.Add(Take(id));
                racks.Add(r);
            }
            var deckTop = new List<string>();
            if (sc.DeckTop != null) foreach (var id in sc.DeckTop) deckTop.Add(Take(id));
            var rest = new List<string>();
            foreach (var id in AllTiles) if (!used.Contains(id)) rest.Add(id);
            rest.Sort((x, y) =>
            {
                int hx = (NumOf(x) * 7 + IndexOfColor(ColorOf(x)) * 5) % 13, hy = (NumOf(y) * 7 + IndexOfColor(ColorOf(y)) * 5) % 13;
                return hx != hy ? hx - hy : string.CompareOrdinal(x, y);
            });
            var starter = (d + 1) % 4;
            for (var k = 0; k < 4; k++)
            {
                var r = racks[k];
                var need = k == starter ? 22 : 21;
                if (r.Count > need) throw new SpecException("E_SPEC_RACK " + k);
                while (r.Count < need) { r.Add(rest[0]); rest.RemoveAt(0); }
            }
            int S_(int x) => (ind + x) % 15;
            var stacks = new List<string>[15];
            string extra = null;
            for (var k = 0; k < 4; k++)
            {
                int seat = (d + 1 + k) % 4, p = 0, first = S_(1 + k);
                var r = racks[seat];
                if (first == S_(1)) extra = r[p++];
                stacks[first] = Reversed(r.GetRange(p, 7)); p += 7;
                stacks[S_(5 + k)] = Reversed(r.GetRange(p, 7)); p += 7;
                stacks[S_(9 + k)] = Reversed(r.GetRange(p, 7));
            }
            var deck = new List<string>(deckTop);
            deck.AddRange(rest);
            if (deck.Count != 6 + 7 + 7) throw new SpecException("E_SPEC_COUNT " + deck.Count);
            int c13 = S_(13), c14 = S_(14), right = Math.Max(c13, c14), left = Math.Min(c13, c14);
            var indSt = deck.GetRange(0, 6);
            indSt.Insert(lv, sc.Indicator);
            stacks[ind] = indSt; stacks[right] = deck.GetRange(6, 7); stacks[left] = deck.GetRange(13, 7);
            var order = new List<string>();
            foreach (var s in stacks) order.AddRange(s);
            order.Add(extra);
            return new DealRig { Dealer = d, Dice = new[] { a, b }, Order = order };
        }

        private static List<string> Reversed(List<string> l) { l.Reverse(); return l; }
        private static int IndexOfColor(string c) { for (var i = 0; i < Colors.Count; i++) if (Colors[i] == c) return i; return -1; }

        private static Dictionary<string, object> StartHand(MatchState s, DealRig rig, List<GameEvent> ev)
        {
            if (!(s.Status == StatusLobby || s.Status == StatusHandOver)) return Err("E_STATUS");
            s.HandNo += 1;
            s.Hand = DealHand(s, rig);
            s.Status = StatusPlaying;
            var h = s.Hand;
            var counts = new List<object>();
            foreach (var r in h.Racks) counts.Add(r.Count);
            ev.Add(new GameEvent("handStart").With("handNo", s.HandNo).With("dealer", h.Deal.Dealer).With("dice", V4Tree.Ints(h.Deal.Dice))
                .With("indicator", h.Indicator).With("okey", h.Okey.ToTree()).With("starter", h.Turn.Seat).With("deck", h.Deck.Count).With("counts", counts));
            ev.Add(new GameEvent("turn").With("seat", h.Turn.Seat).With("phase", h.Turn.Phase).With("n", h.Turn.N));
            return null;
        }

        /// <summary>Tests: builds a hand in progress directly. A repeated id throws E_SPEC_DUP.</summary>
        public static MatchState FromSpec(HandSpec sp)
        {
            var s = NewMatch(sp.Opts ?? new MatchOptions(), sp.Seed.HasValue && sp.Seed.Value != 0 ? sp.Seed.Value : 1u);
            s.Status = StatusPlaying;
            s.HandNo = sp.HandNo.HasValue && sp.HandNo.Value != 0 ? sp.HandNo.Value : 1;
            s.Dealer = sp.Dealer ?? 3;
            if (sp.Totals != null) s.Totals = Four(sp.Totals);
            if (sp.Seats != null) { s.Seats = new SeatInfo[sp.Seats.Length]; for (var i = 0; i < sp.Seats.Length; i++) s.Seats[i] = sp.Seats[i].Clone(); }
            var h = BlankHand();
            var used = new HashSet<string>();
            string Take(string id)
            {
                if (!IsTile(id)) throw new SpecException("E_SPEC_TILE " + (id ?? "undefined"));
                if (used.Contains(id)) throw new SpecException("E_SPEC_DUP " + id);
                used.Add(id);
                return id;
            }
            List<string> TakeAll(List<List<string>> lists, int k)
            {
                var r = new List<string>();
                if (lists != null && k < lists.Count && lists[k] != null) foreach (var id in lists[k]) r.Add(Take(id));
                return r;
            }
            h.Indicator = Take(sp.Indicator);
            h.Okey = OkeyFromIndicator(h.Indicator);
            int k2;
            for (k2 = 0; k2 < 4; k2++) h.Racks[k2] = TakeAll(sp.Racks, k2);
            for (k2 = 0; k2 < 4; k2++) h.Discards[k2] = TakeAll(sp.Discards, k2);
            if (sp.Melds != null)
                foreach (var m in sp.Melds)
                {
                    var tiles = new List<string>();
                    foreach (var id in m.Tiles) tiles.Add(Take(id));
                    var an = AnalyzeMeld(h.Okey, m.Kind, tiles);
                    if (!an.Ok) throw new SpecException("E_SPEC_MELD " + string.Join(",", tiles));
                    var placedBy = new List<int>();
                    foreach (var unused in tiles) placedBy.Add(m.Owner);
                    h.Melds.Add(new TableMeld { Id = "m" + (++h.MeldSeq), Owner = m.Owner, Kind = m.Kind, Tiles = tiles, PlacedBy = placedBy });
                }
            if (sp.Deck != null) { h.Deck = new List<string>(); foreach (var id in sp.Deck) h.Deck.Add(Take(id)); }
            var rest = new List<string>();
            foreach (var id in AllTiles) if (!used.Contains(id)) rest.Add(id);
            if (sp.Deck == null)
            {
                var n = sp.DeckCount ?? Const.Deck;
                n = Math.Max(0, Math.Min(n, rest.Count));
                h.Deck = rest.GetRange(0, n);
                rest = rest.GetRange(n, rest.Count - n);
            }
            h.Out = rest;                                                                              // tests only: tiles not in play
            if (sp.Opened != null) h.Opened = Four(sp.Opened);
            if (sp.OpenMode != null) { h.OpenMode = new string[4]; for (k2 = 0; k2 < 4 && k2 < sp.OpenMode.Length; k2++) h.OpenMode[k2] = sp.OpenMode[k2]; }
            for (k2 = 0; k2 < 4; k2++) if (h.Opened[k2] != 0 && string.IsNullOrEmpty(h.OpenMode[k2])) h.OpenMode[k2] = KindRun;
            for (k2 = 0; k2 < 4; k2++) h.OpenedTurn[k2] = h.Opened[k2] != 0 ? 0 : -1;
            if (sp.Pen != null) h.Pen = Four(sp.Pen);
            h.Turn = NewTurn(sp.TurnSeat ?? 0, string.IsNullOrEmpty(sp.TurnPhase) ? PhaseDraw : sp.TurnPhase, sp.TurnN.HasValue && sp.TurnN.Value != 0 ? sp.TurnN.Value : 5);
            s.Hand = h;
            return s;
        }

        // ---------- Helpers ----------
        private static int[] Four(int[] xs) { var a = new int[4]; for (var i = 0; i < 4 && i < xs.Length; i++) a[i] = xs[i]; return a; }
        private static Dictionary<string, object> Err(string code, string key = null, object value = null)
        {
            var e = new Dictionary<string, object> { { "error", code } };
            if (key != null) e[key] = value;
            return e;
        }
        private static TableMeld MeldById(HandState h, string id) { foreach (var m in h.Melds) if (m.Id == id) return m; return null; }
        public static int PartnerOf(MatchState s, int k) => s.Opts.Mode == "pairs" ? (k + 2) % 4 : -1;
        public static int LeftOf(int k) => (k + 3) % 4;      // R-30 the left player = the previous seat
        public static int NextOf(int k) => (k + 1) % 4;

        private static void AddPenalty(MatchState s, PenaltyEntry p, List<GameEvent> ev)
        {
            if (!s.Opts.Penalties) return;                                                             // R-62
            var h = s.Hand;
            h.Pen[p.Seat] += p.Amount;
            h.PenLog.Add(p);
            ev.Add(new GameEvent("penalty").With("seat", p.Seat).With("code", p.Code).With("amount", p.Amount).With("by", p.By).With("tile", p.Tile).With("total", h.Pen[p.Seat]));
        }
        private static void Snap(HandState h)
        {
            var t = h.Turn;
            if (t.Snap != null) return;
            var k = t.Seat;
            t.Snap = new TurnSnapshot
            {
                Rack = new List<string>(h.Racks[k]), Melds = V4Tree.CloneMelds(h.Melds), MeldSeq = h.MeldSeq, Opened = h.Opened[k], OpenMode = h.OpenMode[k],
                OpenedTurn = h.OpenedTurn[k], Pending = V4Tree.ClonePenalties(t.Pending), SideMust = t.SideMust
            };
        }
        private static bool Restore(HandState h)
        {
            var t = h.Turn;
            var p = t.Snap;
            var k = t.Seat;
            if (p == null) return false;
            h.Racks[k] = p.Rack; h.Melds = p.Melds; h.MeldSeq = p.MeldSeq; h.Opened[k] = p.Opened; h.OpenMode[k] = p.OpenMode; h.OpenedTurn[k] = p.OpenedTurn;
            t.Pending = p.Pending; t.SideMust = p.SideMust; t.Snap = null;
            return true;
        }

        // R-41 işlek: a tile that can be added to any meld on the table or can take the place of an okey.
        private static List<string> AddSides(HandState h, TableMeld meld, string tile)
        {
            var o = new List<string>();
            if (meld.Kind == KindPair) return o;
            if (meld.Kind == KindGroup) { if (GroupKeepsOkeys(h.Okey, meld.Tiles, Concat(meld.Tiles, tile))) o.Add("post"); return o; }
            if (AnalyzeMeld(h.Okey, KindRun, Prepend(tile, meld.Tiles)).Ok) o.Add("pre");
            if (AnalyzeMeld(h.Okey, KindRun, Concat(meld.Tiles, tile)).Ok) o.Add("post");
            return o;
        }
        private static bool SwapFits(HandState h, TableMeld meld, int index, string tile)
        {
            if (index < 0 || index >= meld.Tiles.Count) return false;
            if (meld.Kind == KindPair || IsOkey(h.Okey, tile) || !IsOkey(h.Okey, meld.Tiles[index])) return false;
            var t2 = new List<string>(meld.Tiles);
            t2[index] = tile;
            if (meld.Kind == KindGroup)
            {                                                                                                      // R08/R09: only the tile the okey stands for, and the other okey keeps its face
                var was = AnalyzeMeld(h.Okey, KindGroup, meld.Tiles);
                var real = RealOf(h.Okey, tile);
                if (!was.Ok || was.Repr[index].C != real.C || was.Repr[index].N != real.N) return false;
                return GroupKeepsOkeys(h.Okey, meld.Tiles, t2);
            }
            return AnalyzeMeld(h.Okey, meld.Kind, t2).Ok;
        }
        // R08: a group's okey keeps the face it took when the group was laid; a change that would give it another colour is refused.
        private static bool GroupKeepsOkeys(ColorNum ok, List<string> before, List<string> after)
        {
            var a = AnalyzeMeld(ok, KindGroup, after);
            if (!a.Ok) return false;
            var b = AnalyzeMeld(ok, KindGroup, before);
            if (!b.Ok) return true;
            for (var i = 0; i < before.Count; i++)
                if (IsOkey(ok, before[i]) && IsOkey(ok, after[i]) && (a.Repr[i].C != b.Repr[i].C || a.Repr[i].N != b.Repr[i].N)) return false;
            return true;
        }
        private static bool IsIslek(HandState h, string tile)
        {
            for (var m = 0; m < h.Melds.Count; m++)
            {
                var g = h.Melds[m];
                if (g.Kind == KindPair) continue;
                if (AddSides(h, g, tile).Count > 0) return true;
                for (var i = 0; i < g.Tiles.Count; i++) if (SwapFits(h, g, i, tile)) return true;
            }
            return false;
        }
        private static int TileValueInMeld(HandState h, TableMeld meld, string id)
        {
            var a = AnalyzeMeld(h.Okey, meld.Kind, meld.Tiles);
            var i = meld.Tiles.IndexOf(id);
            if (a.Ok && i >= 0 && a.Repr != null) return a.Repr[i].N;
            return RealOf(h.Okey, id).N;
        }
        private static List<string> Concat(List<string> tiles, string tile) { var l = new List<string>(tiles); l.Add(tile); return l; }
        private static List<string> Prepend(string tile, List<string> tiles) { var l = new List<string>(tiles.Count + 1) { tile }; l.AddRange(tiles); return l; }

        // R-71 tile to discard on timeout: not okey / işlek / the side tile; fewest connections on the rack; then higher value; then lower id.
        public static string AutoDiscardTile(MatchState s, int seat)
        {
            var h = s.Hand;
            var rack = h.Racks[seat];
            var ok = h.Okey;
            var t = h.Turn;
            int Conn(string id)
            {
                if (IsOkey(ok, id)) return 99;
                var r = RealOf(ok, id);
                var n = 0;
                for (var i = 0; i < rack.Count; i++)
                {
                    var o = rack[i];
                    if (o == id) continue;
                    if (IsOkey(ok, o)) { n++; continue; }
                    var q = RealOf(ok, o);
                    if (q.C == r.C && q.N != r.N && Math.Abs(q.N - r.N) <= 2) n++;
                    else if (q.N == r.N && q.C != r.C) n++;
                }
                return n;
            }
            string best = null;
            int b0 = 0, b1 = 0, b2 = 0;
            for (var i = 0; i < rack.Count; i++)
            {
                var id = rack[i];
                if (id == t.SideTake) continue;
                var tier = 0;
                if (IsOkey(ok, id)) tier = 3; else if (s.Opts.Isle && IsIslek(h, id)) tier = 2;
                int r0 = tier, r1 = Conn(id), r2 = -HandValueOf(ok, id);
                bool less;
                if (best == null) less = true;
                else if (r0 != b0) less = r0 < b0;
                else if (r1 != b1) less = r1 < b1;
                else if (r2 != b2) less = r2 < b2;
                else less = string.CompareOrdinal(id, best) < 0;
                if (less) { best = id; b0 = r0; b1 = r1; b2 = r2; }
            }
            return best;
        }

        // ---------- Actions ----------
        /// <summary>apply(S, action) → {ok, error, state, events}. The input is not changed; the returned state is a new object.</summary>
        public static ApplyResult Apply(MatchState s0, GameAction a)
        {
            var s = s0.Clone();
            var ev = new List<GameEvent>();
            var r = Step(s, a, ev);
            if (r != null) return new ApplyResult { Ok = false, Error = (string)r["error"], Detail = r, State = s0, Events = new List<GameEvent>() };
            return new ApplyResult { Ok = true, Error = null, State = s, Events = ev };
        }

        private static Dictionary<string, object> Step(MatchState s, GameAction a, List<GameEvent> ev)
        {
            if (a == null || string.IsNullOrEmpty(a.T)) return Err("E_ACTION");
            if (a.T == "startHand" || a.T == "nextHand") return StartHand(s, a.Rig, ev);
            if (a.T == "leave") return DoLeave(s, a, ev);
            if (a.T == "takeSeat") return DoTakeSeat(s, a, ev);
            if (s.Status != StatusPlaying) return Err("E_HAND_OVER");
            var h = s.Hand;
            var t = h.Turn;
            if (a.Seat != t.Seat) return Err("E_NOT_YOUR_TURN");
            switch (a.T)
            {
                case "draw": return DoDraw(s, a, ev);
                case "returnSide": return DoReturnSide(s, ev);
                case "open": return DoOpen(s, a, ev);
                case "lay": return DoLay(s, a, ev);
                case "add": return DoAdd(s, a, ev);
                case "swap": return DoSwap(s, a, ev);
                case "undo": return DoUndo(s, ev);
                case "discard": return DoDiscard(s, a.Tile, ev);
                case "timeout": return DoTimeout(s, ev);
            }
            return Err("E_ACTION");
        }

        // R-30..R-33
        private static Dictionary<string, object> DoDraw(MatchState s, GameAction a, List<GameEvent> ev)
        {
            var h = s.Hand;
            var t = h.Turn;
            var k = t.Seat;
            if (t.Phase != PhaseDraw) return Err("E_PHASE");
            string id;
            var side = a.Src == "side";
            if (side)
            {
                if (t.SideReturned) return Err("E_SIDE_RETURNED");                                      // R-33
                var pile = h.Discards[LeftOf(k)];
                if (pile.Count == 0) return Err("E_SIDE_EMPTY");
                id = pile[pile.Count - 1];
                pile.RemoveAt(pile.Count - 1);
                t.SideTake = id; t.SideMust = h.Opened[k] == 0;                                         // R-31
            }
            else
            {
                if (h.Deck.Count == 0) return Err("E_DECK_EMPTY");
                id = h.Deck[0];
                h.Deck.RemoveAt(0);
            }
            h.Racks[k].Add(id); t.Drawn = id; t.Phase = PhasePlay;
            ev.Add(new GameEvent("drew").With("seat", k).With("src", side ? "side" : "deck").With("tile", id).With("from", side ? (object)LeftOf(k) : null)
                .With("deck", h.Deck.Count).With("mustOpen", t.SideMust));
            return null;
        }

        // R-32 return the side tile: every action of the turn is undone (D-10), the tile goes back on the pile, the player may only draw from the deck.
        private static Dictionary<string, object> DoReturnSide(MatchState s, List<GameEvent> ev)
        {
            var h = s.Hand;
            var t = h.Turn;
            var k = t.Seat;
            if (t.Phase != PhasePlay || t.SideTake == null) return Err("E_RETURN_INVALID");
            Restore(h);
            if (!h.Racks[k].Remove(t.SideTake)) return Err("E_RETURN_INVALID");
            h.Discards[LeftOf(k)].Add(t.SideTake);
            ev.Add(new GameEvent("sideReturned").With("seat", k).With("tile", t.SideTake).With("to", LeftOf(k)));
            t.SideTake = null; t.SideMust = false; t.SideReturned = true; t.Phase = PhaseDraw; t.Drawn = null; t.Pending = new List<PenaltyEntry>(); t.Snap = null;
            return null;
        }

        private sealed class MeldCheck
        {
            public Dictionary<string, object> Error;
            public List<MeldAnalysis> List;
            public string Mode;
            public List<string> Used;           // Object.keys(seen): insertion order
            public HashSet<string> UsedSet;
        }

        private static MeldCheck CheckMelds(HandState h, int k, List<MeldSpec> melds)
        {
            if (melds == null || melds.Count == 0) return new MeldCheck { Error = Err("E_MELD_INVALID", "index", -1) };
            var rack = h.Racks[k];
            var seen = new HashSet<string>();
            var seenOrder = new List<string>();
            var o = new List<MeldAnalysis>();
            bool pair = false, run = false;
            for (var m = 0; m < melds.Count; m++)
            {
                var g = melds[m];
                var tiles = g.Tiles ?? new List<string>();
                for (var i = 0; i < tiles.Count; i++)
                {
                    var id = tiles[i];
                    if (!rack.Contains(id)) return new MeldCheck { Error = Err("E_TILE_NOT_IN_RACK", "tile", id) };
                    if (seen.Contains(id)) return new MeldCheck { Error = Err("E_MELD_INVALID", "index", m) };
                    seen.Add(id); seenOrder.Add(id);
                }
                var an = AnalyzeMeld(h.Okey, g.Kind, tiles);
                if (!an.Ok) return new MeldCheck { Error = Err("E_MELD_INVALID", "index", m) };
                if (g.Kind == KindPair) pair = true; else run = true;
                o.Add(an);
            }
            if (pair && run) return new MeldCheck { Error = Err("E_MELD_MIX") };
            if (rack.Count - seenOrder.Count < 1) return new MeldCheck { Error = Err("E_LAST_TILE") };                  // R-35
            return new MeldCheck { List = o, Mode = pair ? KindPair : KindRun, Used = seenOrder, UsedSet = seen };
        }
        private static List<string> PlaceMelds(HandState h, int k, List<MeldSpec> melds)
        {
            var ids = new List<string>();
            foreach (var g in melds)
            {
                var placedBy = new List<int>();
                foreach (var unused in g.Tiles) placedBy.Add(k);
                var m = new TableMeld { Id = "m" + (++h.MeldSeq), Owner = k, Kind = g.Kind, Tiles = new List<string>(g.Tiles), PlacedBy = placedBy };
                h.Melds.Add(m); ids.Add(m.Id);
                foreach (var id in g.Tiles) h.Racks[k].Remove(id);
            }
            return ids;
        }
        private static List<object> MeldTrees(HandState h, List<string> ids)
        {
            var l = new List<object>();
            foreach (var id in ids) l.Add(MeldById(h, id).ToTree());
            return l;
        }

        /// <summary>
        /// R-20, R-21, R-64 opening threshold. Katlamalı: the previous opening of the same mode + 1 (series and pairs independent).
        /// In pairs with katlamaPartner off only the other team's openings raise it.
        /// </summary>
        public static int OpenNeed(MatchState s, int k, string mode)
        {
            var h = s.Hand;
            var need = mode == KindPair ? Const.OpenPairMin : Const.OpenRunMin;
            if (!s.Opts.Katlama) return need;
            for (var j = 0; j < 4; j++)
            {
                if (j == k || h.Opened[j] == 0 || h.OpenMode[j] != mode) continue;
                if (s.Opts.Mode == "pairs" && !s.Opts.KatlamaPartner && j == PartnerOf(s, k)) continue;
                if (h.Opened[j] + 1 > need) need = h.Opened[j] + 1;
            }
            return need;
        }

        // R-20..R-24, R-53
        private static Dictionary<string, object> DoOpen(MatchState s, GameAction a, List<GameEvent> ev)
        {
            var h = s.Hand;
            var t = h.Turn;
            var k = t.Seat;
            if (t.Phase != PhasePlay) return Err("E_PHASE");
            if (h.Opened[k] != 0) return Err("E_ALREADY_OPENED");
            var c = CheckMelds(h, k, a.Melds);
            if (c.Error != null) return c.Error;
            if (t.SideMust && !c.UsedSet.Contains(t.SideTake)) return Err("E_SIDE_MUST_OPEN");                    // R-31
            var total = 0;
            if (c.Mode == KindPair) total = a.Melds.Count; else foreach (var x in c.List) total += x.Value;
            var need = OpenNeed(s, k, c.Mode);                                                                     // R-53 the bar applies to finishing from the hand too
            if (total < need)
            {                                                                                                      // R-23 short opening
                ev.Add(new GameEvent("openShort").With("seat", k).With("mode", c.Mode).With("value", total).With("need", need).With("tiles", V4Tree.Strings(c.Used)));
                AddPenalty(s, new PenaltyEntry { Seat = k, Code = PenOpenShort, Amount = Const.Penalty, By = k, Tile = null, Value = total }, ev);
                return null;
            }
            Snap(h);
            var ids = PlaceMelds(h, k, a.Melds);
            h.Opened[k] = total; h.OpenMode[k] = c.Mode; h.OpenedTurn[k] = t.N;
            if (t.SideTake != null && c.UsedSet.Contains(t.SideTake) && t.SideMust)
            {                                                                                                      // R-43 the tile that opened
                TableMeld m = null;
                foreach (var x in h.Melds) if (x.Tiles.Contains(t.SideTake)) m = x;
                var v = TileValueInMeld(h, m, t.SideTake);
                t.Pending.Add(new PenaltyEntry { Seat = LeftOf(k), Code = PenOpenedWith, Amount = v * Const.OpenedWithX, By = k, Tile = t.SideTake, Value = v });
            }
            t.SideMust = false;
            ev.Add(new GameEvent("opened").With("seat", k).With("mode", c.Mode).With("value", total).With("melds", MeldTrees(h, ids)));
            return null;
        }

        // R-24 lay: a series opener lays runs/groups and pairs (in the opening turn too); a pairs opener lays pairs only
        private static Dictionary<string, object> DoLay(MatchState s, GameAction a, List<GameEvent> ev)
        {
            var h = s.Hand;
            var t = h.Turn;
            var k = t.Seat;
            if (t.Phase != PhasePlay) return Err("E_PHASE");
            if (h.Opened[k] == 0) return Err("E_NOT_OPENED");
            var c = CheckMelds(h, k, a.Melds);
            if (c.Error != null) return c.Error;
            if (h.OpenMode[k] == KindPair && c.Mode != KindPair) return Err("E_OPEN_MODE");
            Snap(h);
            var ids = PlaceMelds(h, k, a.Melds);
            ev.Add(new GameEvent("laid").With("seat", k).With("melds", MeldTrees(h, ids)));
            return null;
        }

        // R-25 işle
        private static Dictionary<string, object> DoAdd(MatchState s, GameAction a, List<GameEvent> ev)
        {
            var h = s.Hand;
            var t = h.Turn;
            var k = t.Seat;
            if (t.Phase != PhasePlay) return Err("E_PHASE");
            if (!s.Opts.Isle) return Err("E_ISLE_DISABLED");
            if (h.Opened[k] == 0) return Err("E_NOT_OPENED");
            if (!h.Racks[k].Contains(a.Tile)) return Err("E_TILE_NOT_IN_RACK", "tile", a.Tile);
            if (h.Racks[k].Count < 2) return Err("E_LAST_TILE");
            var m = MeldById(h, a.Meld);
            if (m == null) return Err("E_MELD_NOT_FOUND");
            var sides = AddSides(h, m, a.Tile);
            var side = m.Kind == KindGroup ? "post" : a.Side;
            if (!sides.Contains(side)) return Err("E_ISLE_INVALID");
            Snap(h);
            h.Racks[k].Remove(a.Tile);
            if (side == "pre") { m.Tiles.Insert(0, a.Tile); m.PlacedBy.Insert(0, k); } else { m.Tiles.Add(a.Tile); m.PlacedBy.Add(k); }
            ev.Add(new GameEvent("added").With("seat", k).With("tile", a.Tile).With("meld", m.Id).With("side", side).With("owner", m.Owner));
            return null;
        }

        // R-26 taking the okey
        private static Dictionary<string, object> DoSwap(MatchState s, GameAction a, List<GameEvent> ev)
        {
            var h = s.Hand;
            var t = h.Turn;
            var k = t.Seat;
            if (t.Phase != PhasePlay) return Err("E_PHASE");
            if (!s.Opts.Isle) return Err("E_ISLE_DISABLED");
            if (h.Opened[k] == 0) return Err("E_NOT_OPENED");
            if (!h.Racks[k].Contains(a.Tile)) return Err("E_TILE_NOT_IN_RACK", "tile", a.Tile);
            var m = MeldById(h, a.Meld);
            if (m == null) return Err("E_MELD_NOT_FOUND");
            if (!SwapFits(h, m, a.Index, a.Tile))
            {
                var inRange = a.Index >= 0 && a.Index < m.Tiles.Count;
                object need = inRange && IsOkey(h.Okey, m.Tiles[a.Index]) ? AnalyzeMeld(h.Okey, m.Kind, m.Tiles).Repr[a.Index].ToTree() : null;
                return Err("E_SWAP_INVALID", "need", need);
            }
            Snap(h);
            var okeyId = m.Tiles[a.Index];
            var owner = m.PlacedBy[a.Index];
            m.Tiles[a.Index] = a.Tile; m.PlacedBy[a.Index] = k;
            h.Racks[k].Remove(a.Tile); h.Racks[k].Add(okeyId);
            if (owner != k) t.Pending.Add(new PenaltyEntry { Seat = owner, Code = PenOkeyTaken, Amount = Const.Penalty, By = k, Tile = okeyId });   // R-44
            ev.Add(new GameEvent("okeySwapped").With("seat", k).With("tile", a.Tile).With("okey", okeyId).With("meld", m.Id).With("index", a.Index).With("owner", owner));
            return null;
        }

        // R-34 undo
        private static Dictionary<string, object> DoUndo(MatchState s, List<GameEvent> ev)
        {
            var h = s.Hand;
            var t = h.Turn;
            if (t.Phase != PhasePlay || t.Snap == null) return Err("E_NO_UNDO");
            Restore(h);
            ev.Add(new GameEvent("undone").With("seat", t.Seat));
            return null;
        }

        // R-36..R-37, R-40..R-42
        private static Dictionary<string, object> DoDiscard(MatchState s, string tile, List<GameEvent> ev)
        {
            var h = s.Hand;
            var t = h.Turn;
            var k = t.Seat;
            var ok = h.Okey;
            if (t.Phase != PhasePlay) return Err("E_PHASE");
            if (!h.Racks[k].Contains(tile)) return Err("E_TILE_NOT_IN_RACK", "tile", tile);
            if (t.SideTake != null && tile == t.SideTake) return Err("E_SIDE_DISCARD");                           // R-32
            if (t.SideMust && h.Racks[k].Contains(t.SideTake)) return Err("E_SIDE_MUST_OPEN");                     // R-31
            h.Racks[k].Remove(tile);
            if (h.Racks[k].Count == 0)
            {
                if (h.Opened[k] == 0) return Err("E_NOT_OPENED");
                CommitPending(s, ev);
                h.FinishTile = tile;
                ev.Add(new GameEvent("finished").With("seat", k).With("tile", tile));
                return EndHand(s, k, ev);
            }
            h.Discards[k].Add(tile);
            ev.Add(new GameEvent("discarded").With("seat", k).With("tile", tile));
            if (IsOkey(ok, tile)) AddPenalty(s, new PenaltyEntry { Seat = k, Code = PenOkeyDiscard, Amount = Const.Penalty, By = k, Tile = tile }, ev);       // R-42
            else if (s.Opts.Isle && IsIslek(h, tile)) AddPenalty(s, new PenaltyEntry { Seat = k, Code = PenIslek, Amount = Const.Penalty, By = k, Tile = tile }, ev);   // R-41
            CommitPending(s, ev);
            if (h.Deck.Count == 0) return EndHand(s, -1, ev);                                                     // R-55 deck out
            var n = NextOf(k);
            h.Turn = NewTurn(n, PhaseDraw, t.N + 1);
            ev.Add(new GameEvent("turn").With("seat", n).With("phase", PhaseDraw).With("n", t.N + 1));
            return null;
        }
        private static void CommitPending(MatchState s, List<GameEvent> ev)
        {
            var t = s.Hand.Turn;
            foreach (var p in t.Pending) AddPenalty(s, p, ev);
            t.Pending = new List<PenaltyEntry>();
            t.Snap = null;
        }

        // R-70..R-71 timeout auto-play
        private static Dictionary<string, object> DoTimeout(MatchState s, List<GameEvent> ev)
        {
            var h = s.Hand;
            var t = h.Turn;
            var k = t.Seat;
            Dictionary<string, object> r;
            ev.Add(new GameEvent("auto").With("seat", k).With("phase", t.Phase));
            if (t.Phase == PhasePlay && t.SideMust && h.Racks[k].Contains(t.SideTake)) { r = DoReturnSide(s, ev); if (r != null) return r; }
            if (h.Turn.Phase == PhaseDraw) { r = DoDraw(s, new GameAction { T = "draw", Seat = k, Src = "deck" }, ev); if (r != null) return r; }
            var id = AutoDiscardTile(s, k);
            if (id == null && h.Turn.SideTake != null)
            {                                                                                                      // only the side tile is left (R-32 forbids discarding it): return it, draw from the deck
                r = DoReturnSide(s, ev); if (r != null) return r;
                r = DoDraw(s, new GameAction { T = "draw", Seat = k, Src = "deck" }, ev); if (r != null) return r;
                id = AutoDiscardTile(s, k);
            }
            return DoDiscard(s, id, ev);
        }

        // R-65 taking a seat: a bot's or leaver's seat passes to a new player, who continues with the seat's total; the "left" mark goes.
        private static Dictionary<string, object> DoTakeSeat(MatchState s, GameAction a, List<GameEvent> ev)
        {
            var st = a.Seat >= 0 && a.Seat < s.Seats.Length ? s.Seats[a.Seat] : null;
            if (st == null || !st.Bot) return Err("E_SEAT_NOT_BOT");
            if (s.Status == StatusMatchOver) return Err("E_STATUS");
            st.Bot = false; st.Left = false; st.LeftSeq = 0;
            ev.Add(new GameEvent("seatTaken").With("seat", a.Seat).With("total", s.Totals[a.Seat]));
            return null;
        }

        // R-60 leaving the table
        private static Dictionary<string, object> DoLeave(MatchState s, GameAction a, List<GameEvent> ev)
        {
            var st = a.Seat >= 0 && a.Seat < s.Seats.Length ? s.Seats[a.Seat] : null;
            if (st == null || st.Left) return Err("E_LEAVE_INVALID");
            st.Left = true; st.Bot = true; st.LeftSeq = ++s.LeaveSeq;
            ev.Add(new GameEvent("seatLeft").With("seat", a.Seat).With("botTakesOver", s.Status == StatusPlaying || s.Status == StatusHandOver));
            return null;
        }

        // ---------- Scoring (R-50..R-57) ----------
        private static Dictionary<string, object> EndHand(MatchState s, int fin, List<GameEvent> ev)
        {
            var h = s.Hand;
            var ok = h.Okey;
            int k;
            var res = new HandResult { Reason = fin >= 0 ? "finish" : "deckOut", Finisher = fin, Elden = false, OkeyFinish = false, PairFinish = false, Mul = 1, Pen = (int[])h.Pen.Clone(), TeamScore = null };
            if (fin >= 0)
            {
                res.Elden = h.OpenedTurn[fin] == h.Turn.N;                                                         // R-53
                res.OkeyFinish = IsOkey(ok, h.FinishTile);                                                         // R-54
                res.PairFinish = h.OpenMode[fin] == KindPair;                                                      // R-54
                res.Mul = (res.Elden ? 2 : 1) * (res.OkeyFinish ? 2 : 1) * (res.PairFinish ? 2 : 1);
                for (k = 0; k < 4; k++)
                {
                    if (k == fin) { res.Base[k] = Const.Finish; res.Score[k] = Const.Finish * res.Mul + h.Pen[k]; continue; }
                    if (k == PartnerOf(s, fin)) { res.Base[k] = 0; res.Score[k] = h.Pen[k]; continue; }          // R-57
                    var bas = Const.NotOpened;
                    if (h.Opened[k] != 0) { bas = 0; for (var i = 0; i < h.Racks[k].Count; i++) bas += HandValueOf(ok, h.Racks[k][i]); }
                    res.Base[k] = bas;
                    res.PairX[k] = h.OpenMode[k] == KindPair ? 2 : 1;                                              // R-56
                    res.Score[k] = bas * res.Mul * res.PairX[k] + h.Pen[k];                                        // R-58 penalties are not multiplied
                }
            }
            else
            {
                for (k = 0; k < 4; k++)
                {                                                                                                  // R-55 deck out: everyone writes the rack, no multiplier, no −101
                    var b2 = Const.NotOpened;
                    if (h.Opened[k] != 0) { b2 = 0; for (var i2 = 0; i2 < h.Racks[k].Count; i2++) b2 += HandValueOf(ok, h.Racks[k][i2]); }
                    res.Base[k] = b2; res.PairX[k] = h.OpenMode[k] == KindPair ? 2 : 1;
                    res.Score[k] = b2 * res.PairX[k] + h.Pen[k];
                }
            }
            if (s.Opts.Mode == "pairs") res.TeamScore = new[] { res.Score[0] + res.Score[2], res.Score[1] + res.Score[3] };
            for (k = 0; k < 4; k++) s.Totals[k] += res.Score[k];
            h.Result = res; h.Turn.Phase = PhaseOver;
            s.History.Add(new HistoryEntry { HandNo = s.HandNo, Score = (int[])res.Score.Clone(), Reason = res.Reason, Finisher = fin, Mul = res.Mul });
            ev.Add(new GameEvent("handEnd").With("handNo", s.HandNo).With("result", res.ToTree()).With("totals", V4Tree.Ints(s.Totals)).With("racks", V4Tree.StringLists(h.Racks)));
            if (s.HandNo >= s.Opts.Hands)
            {
                s.Status = StatusMatchOver;
                s.Ranking = Ranking(s);
                ev.Add(new GameEvent("matchEnd").With("ranking", V4Tree.Ranking(s.Ranking)).With("totals", V4Tree.Ints(s.Totals)));
            }
            else s.Status = StatusHandOver;
            return null;
        }

        /// <summary>R-59 ranking: lower total first; ties share the place (1, 1, 3); leavers last, the later leaver ahead (solo only, R-60).</summary>
        public static List<RankUnit> Ranking(MatchState s)
        {
            var units = new List<RankUnit>();
            if (s.Opts.Mode == "pairs")
            {
                var teams = new[] { new[] { 0, 2 }, new[] { 1, 3 } };
                for (var i = 0; i < teams.Length; i++)
                {
                    var t = teams[i];
                    var leavers = new List<int>();
                    foreach (var x in t) if (s.Seats[x].Left) leavers.Add(x);
                    units.Add(new RankUnit { Team = i, Seats = t, Total = s.Totals[t[0]] + s.Totals[t[1]], Left = false, LeftSeq = 0, Leavers = leavers.ToArray() });   // R-60: in pairs the team is not ranked last
                }
            }
            else for (var k = 0; k < 4; k++) units.Add(new RankUnit { Seats = new[] { k }, Total = s.Totals[k], Left = s.Seats[k].Left, LeftSeq = s.Seats[k].LeftSeq });
            StableSort(units, (a, b) =>
            {
                if (a.Left != b.Left) return a.Left ? 1 : -1;
                if (a.Left) return b.LeftSeq - a.LeftSeq;
                if (a.Total != b.Total) return a.Total - b.Total;
                return a.Seats[0] - b.Seats[0];
            });
            for (var i = 0; i < units.Count; i++)
            {
                var u = units[i];
                var p = i > 0 ? units[i - 1] : null;
                u.Rank = i > 0 && !u.Left && !p.Left && p.Total == u.Total ? p.Rank : i + 1;
            }
            return units;
        }

        /// <summary>Stable insertion sort: Array.prototype.sort is stable in rules.js / bots.js, List.Sort is not.</summary>
        internal static void StableSort<T>(List<T> list, Comparison<T> cmp)
        {
            for (var i = 1; i < list.Count; i++)
            {
                var x = list[i];
                var j = i - 1;
                while (j >= 0 && cmp(list[j], x) > 0) { list[j + 1] = list[j]; j--; }
                list[j + 1] = x;
            }
        }

        // ---------- View helpers (client / server) ----------
        public static LegalMoves Legal(MatchState s, int seat)
        {
            var o = new LegalMoves();
            if (s.Status != StatusPlaying) return o;
            var h = s.Hand;
            var t = h.Turn;
            if (t.Seat != seat) return o;
            if (t.Phase == PhaseDraw) { o.Draw = h.Deck.Count > 0; o.Side = !t.SideReturned && h.Discards[LeftOf(seat)].Count > 0; return o; }
            o.Discard = true; o.Open = h.Opened[seat] == 0; o.Undo = t.Snap != null;
            o.ReturnSide = t.SideTake != null && (h.Racks[seat].Contains(t.SideTake) || t.Snap != null);
            o.Add = h.Opened[seat] != 0 && s.Opts.Isle;
            return o;
        }

        /// <summary>Every one of the 106 ids exactly once across indicator, deck, racks, discards, melds, finish tile and out.</summary>
        public static List<string> Invariants(MatchState s)
        {
            var h = s.Hand;
            var seen = new HashSet<string>();
            var bad = new List<string>();
            void Mark(string id) { if (seen.Contains(id)) bad.Add("dup " + id); seen.Add(id); }
            if (h == null) return bad;
            if (h.Indicator != null) Mark(h.Indicator);
            foreach (var id in h.Deck) Mark(id);
            foreach (var r in h.Racks) foreach (var id in r) Mark(id);
            foreach (var r in h.Discards) foreach (var id in r) Mark(id);
            foreach (var m in h.Melds) foreach (var id in m.Tiles) Mark(id);
            if (h.FinishTile != null) Mark(h.FinishTile);
            if (h.Out != null) foreach (var id in h.Out) Mark(id);
            foreach (var id in AllTiles) if (!seen.Contains(id)) bad.Add("missing " + id);
            return bad;
        }

        // ---------- API wrappers that take the match (rules.js API object) ----------
        public static bool IsIslek(MatchState s, string id) => IsIslek(s.Hand, id);
        public static List<string> AddSides(MatchState s, string meldId, string id) { var m = MeldById(s.Hand, meldId); return m != null ? AddSides(s.Hand, m, id) : new List<string>(); }
        public static bool SwapFits(MatchState s, string meldId, int index, string id) { var m = MeldById(s.Hand, meldId); return m != null && SwapFits(s.Hand, m, index, id); }
        public static MatchState CloneState(MatchState s) => s.Clone();
    }
}
