using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static Galaxy101.Table.TableConstants;

namespace Galaxy101.Table
{
    /// <summary>What the board shows, from the observer's (seat 0) point of view.</summary>
    public sealed class BoardState
    {
        public RackGrid Rack;
        public int[] HandCounts = new int[4];
        public List<int>[] Discards = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
        /// <summary>Melds on the table: owner and tile ids in display order.</summary>
        public List<(int Id, int Owner, int[] Tiles)> Melds = new List<(int, int, int[])>();
        public int Indicator = -1, Stock;
    }

    /// <summary>
    /// The live tile objects (table3d.js sync): every state is reconciled against the existing views, so each physical
    /// tile keeps one object and travels between deck, racks, discard piles and melds. Opponent tiles are anonymous
    /// (face hidden) until they are discarded or melded; then a tile leaves that opponent's rack and shows its face.
    /// The deck is a pool of face-down tiles laid out by <see cref="TableScene.DeckPose"/>.
    /// </summary>
    public sealed class TableBoard
    {
        private readonly TableScene scene;
        private readonly Dictionary<int, TileView> known = new Dictionary<int, TileView>();
        private readonly List<TileView>[] hidden = { new List<TileView>(), new List<TileView>(), new List<TileView>(), new List<TileView>() };
        private readonly Dictionary<TileView, int> meldOf = new Dictionary<TileView, int>();
        private readonly List<GameObject> deckPool = new List<GameObject>();
        private int deckShown = -1, lastStock;
        private float deckFrom, deckTo, deckT0, deckDuration;
        private Pose? spawnHint;
        private readonly Dictionary<int, float> forcedDurations = new Dictionary<int, float>();

        /// <summary>Real okeys (set by the game once the indicator is known): they carry the gold okey mark.</summary>
        public System.Func<int, bool> IsOkey = _ => false;

        /// <summary>A meld's kind and, for a run, the number of its first tile (okeys take the value they stand for).</summary>
        public System.Func<int[], (bool Run, int Start)> Classify = tiles => (false, 1);

        /// <summary>Centre board layout (spec/23); reset every hand.</summary>
        public readonly BoardLayout Layout = new BoardLayout();
        private int indicatorId = -1;

        /// <summary>Effects for landings, the deck spin and the bars.</summary>
        public TableFx Fx;

        /// <summary>The next move of <paramref name="id"/> takes exactly <paramref name="seconds"/> (spec/17: put back 300 ms, taken okey 450 ms).</summary>
        public void ForceDuration(int id, float seconds) => forcedDurations[id] = seconds;

        public TableBoard(TableScene scene) { this.scene = scene; }

        public TileView ViewOf(int id) => known.TryGetValue(id, out var v) ? v : null;
        public int MeldOf(TileView view) => view != null && meldOf.TryGetValue(view, out var id) ? id : -1;
        public IEnumerable<TileView> MeldViews => meldOf.Keys;
        public int TileObjectCount => known.Count + hidden.Sum(h => h.Count) + deckShown;
        public int HiddenCount(int seat) => hidden[seat].Count;
        public int DeckShown => deckShown;

        /// <summary>The next tile drawn from the deck starts here (a dragged ghost) instead of the deck top.</summary>
        public void SpawnFrom(Pose pose) => spawnHint = pose;

        private static readonly RackSlot[] FillOrder = BuildFillOrder();
        /// <summary>Where an opponent's n-th hidden tile sits (centre-out, bottom row first).</summary>
        public static IReadOnlyList<RackSlot> OpponentFillOrder => FillOrder;
        private static RackSlot[] BuildFillOrder()
        {
            // oppAdd(): centre-out, bottom row before top row.
            var order = new List<RackSlot>();
            for (int d = 0; d < 8; d++)
                foreach (int i in d == 0 ? new[] { 7 } : new[] { 7 + d, 7 - d })
                    if (i >= 0 && i < RackGrid.Columns) { order.Add(new RackSlot(1, i)); order.Add(new RackSlot(0, i)); }
            return order.ToArray();
        }

        private TileView Spawn(string code, Pose at)
        {
            var go = scene.Tiles.Create(code, scene.TilesRoot);
            go.transform.SetPositionAndRotation(at.position, at.rotation);
            var view = go.AddComponent<TileView>(); view.Code = code; view.Fx = Fx; return view;
        }

        private void Kill(TileView view) { meldOf.Remove(view); if (view != null) Object.Destroy(view.gameObject); }

        public void Clear()
        {
            foreach (var v in known.Values) Kill(v); known.Clear();
            foreach (var h in hidden) { foreach (var v in h) Kill(v); h.Clear(); }
            meldOf.Clear(); spawnHint = null; Layout.Reset(); indicatorId = -1;
        }

        // ---------- deck ----------
        private bool heapShown;

        public void LayoutDeck(int n)
        {
            n = Mathf.Clamp(n, 0, 106); scene.DeckCount = n; scene.DeckHeap = deckDuration > 0; if (n == deckShown && heapShown == scene.DeckHeap) return; deckShown = n; heapShown = scene.DeckHeap;
            // Deck tiles live under the effects' deck root so the shuffle beat can spin the whole deck.
            var parent = Fx != null && Fx.DeckRoot != null ? Fx.DeckRoot : scene.TilesRoot;
            while (deckPool.Count < n) { var go = scene.Tiles.Create(null, parent); go.name = "deste_" + deckPool.Count; Object.Destroy(go.GetComponent<Collider>()); deckPool.Add(go); }
            for (int j = 0; j < deckPool.Count; j++)
            {
                bool on = j < n; deckPool[j].SetActive(on);
                if (on) { var p = scene.DeckPose(j, n); deckPool[j].transform.localPosition = p.position; deckPool[j].transform.localRotation = p.rotation; }
            }
        }

        /// <summary>Per-frame: the deal counts the deck down linearly (106 → stock); the deck eases left once melds are down.</summary>
        public void Tick()
        {
            float target = Layout.Count > 0 ? TableScene.DeckLeft : 0;
            if (Mathf.Abs(scene.DeckShift - target) > 0.0005f)
            {
                scene.DeckShift = Mathf.Lerp(scene.DeckShift, target, 1 - Mathf.Exp(-3.2f * Time.deltaTime));
                if (Mathf.Abs(scene.DeckShift - target) <= 0.0005f) scene.DeckShift = target;
                int shown = deckShown; deckShown = -1; LayoutDeck(shown);
                if (indicatorId >= 0 && known.TryGetValue(indicatorId, out var ind) && ind != null && !ind.Tweening) ind.Snap(scene.IndicatorPose());
            }
            if (deckDuration <= 0) return;
            float u = Mathf.Clamp01((Time.time - deckT0) / deckDuration);
            if (u >= 1) deckDuration = 0;
            // The deal over, the stock settles into its piles (and the indicator onto the six-tile pile).
            LayoutDeck(Mathf.RoundToInt(Mathf.Lerp(deckFrom, deckTo, u)));
            if (deckDuration <= 0 && indicatorId >= 0 && known.TryGetValue(indicatorId, out var settled) && settled != null && !settled.Tweening) settled.Snap(scene.IndicatorPose());
        }

        // ---------- deal (spec/10 §2) ----------
        /// <summary>
        /// Fly every rack tile from the deck, round-robin from the starter, 24 ms apart after a 750 ms shuffle beat;
        /// the indicator follows 500 ms after the last tile. Returns the seconds until the deal has landed.
        /// </summary>
        public float Deal(BoardState state, int starter)
        {
            Clear(); LayoutDeck(106);
            const float lead = 0.75f, step = 0.024f;
            var mine = new Queue<int>(state.Rack.Tiles.OrderBy(id => Order(state.Rack, id)));
            int index = 0, maxCount = Mathf.Max(state.HandCounts.Max(), state.Rack.Count);
            var deckTop = scene.DeckTop(106);
            for (int j = 0; j < maxCount; j++)
                for (int o = 0; o < 4; o++)
                {
                    int k = (starter + o) % 4;
                    if (k == 0) { if (mine.Count == 0) continue; int id = mine.Dequeue(); var v = Spawn(TileCodes.Code(id), deckTop); v.Id = id; known[id] = v; scene.Tiles.SetOkeyMark(v.gameObject, IsOkey(id)); v.Bar = scene.Tiles.Bar(v.gameObject); v.MoveTo(RackPose(state.Rack, id), TileView.Motion.Deal, lead + index++ * step); }
                    else
                    {
                        if (hidden[k].Count >= state.HandCounts[k]) continue;
                        var v = Spawn(null, deckTop); v.Seat = k; v.OpponentSlot = FreeOpponentSlot(k); hidden[k].Add(v);
                        v.MoveTo(scene.SlotPose(k, v.OpponentSlot.Row, v.OpponentSlot.Index), TileView.Motion.Deal, lead + index++ * step);
                    }
                }
            float dealt = lead + index * step;
            deckFrom = 106; deckTo = state.Stock; deckT0 = Time.time + lead; deckDuration = index * step + 0.2f;
            if (state.Indicator >= 0)
            {
                var ind = Spawn(TileCodes.Code(state.Indicator), deckTop); ind.Id = state.Indicator; known[state.Indicator] = ind;
                ind.MoveTo(scene.IndicatorPose(), TileView.Motion.Travel, dealt + 0.5f, 0, "reveal");
            }
            lastStock = state.Stock;
            return dealt + 1.5f;
        }

        private static int Order(RackGrid rack, int id) { rack.TryFind(id, out var s); return s.Row * RackGrid.Columns + s.Index; }

        private Pose RackPose(RackGrid rack, int id) { rack.TryFind(id, out var s); return scene.SlotPose(0, s.Row, s.Index); }

        private RackSlot FreeOpponentSlot(int seat)
        {
            var used = new HashSet<RackSlot>(hidden[seat].Select(v => v.OpponentSlot));
            foreach (var slot in FillOrder) if (!used.Contains(slot)) return slot;
            return new RackSlot(1, 7);
        }

        /// <summary>
        /// After the deal ceremony: the board's own objects appear exactly where the ceremony left its tiles (racks, deck,
        /// indicator), so the hand-over is invisible.
        /// </summary>
        public void Settle(BoardState state)
        {
            Clear(); deckDuration = 0; deckShown = -1;
            LayoutDeck(state.Stock); lastStock = state.Stock;
            Sync(state, true);
        }

        // ---------- reconcile ----------
        public void Sync(BoardState state, bool instant = false)
        {
            var desired = new Dictionary<int, (Pose Pose, int Seat, bool Rack, int Meld)>();
            foreach (var id in state.Rack.Tiles) desired[id] = (RackPose(state.Rack, id), 0, true, -1);
            for (int k = 0; k < 4; k++)
                for (int j = 0; j < state.Discards[k].Count; j++)
                {
                    int id = state.Discards[k][j];
                    desired[id] = (scene.DiscardPose(k, j, id.ToString()), k, false, -1);
                }
            Layout.Retain(state.Melds.Select(m => m.Id));
            foreach (var meld in state.Melds)
            {
                var (run, start) = Classify(meld.Tiles);
                var (row, column) = Layout.Place(meld.Id, meld.Owner, run, start, meld.Tiles.Length);
                for (int t = 0; t < meld.Tiles.Length; t++) desired[meld.Tiles[t]] = (scene.BoardPose(Mathf.Min(column + t, BoardLayout.Columns - 1), row), meld.Owner, false, meld.Id);
            }
            indicatorId = state.Indicator;
            if (state.Indicator >= 0) desired[state.Indicator] = (scene.IndicatorPose(), 0, false, -1);

            // Tiles I knew that are no longer visible went into an opponent's hand (taken from a discard pile).
            var leftovers = known.Where(p => !desired.ContainsKey(p.Key)).Select(p => p.Value).ToList();
            foreach (var v in leftovers) known.Remove(v.Id);
            meldOf.Clear();

            var deckTop = spawnHint ?? scene.DeckTop(Mathf.Max(lastStock, state.Stock));
            foreach (var pair in desired)
            {
                if (!known.TryGetValue(pair.Key, out var view))
                {
                    int seat = pair.Value.Seat;
                    // A newly visible tile from an opponent: it leaves their hidden rack (discard, meld).
                    if (seat > 0 && hidden[seat].Count > state.HandCounts[seat]) { view = hidden[seat][hidden[seat].Count - 1]; hidden[seat].RemoveAt(hidden[seat].Count - 1); }
                    else view = Spawn(null, deckTop);
                    view.Id = pair.Key; view.Seat = -1; view.Code = TileCodes.Code(pair.Key); scene.Tiles.SetFace(view.gameObject, view.Code);
                    known[pair.Key] = view;
                }
                if (pair.Value.Meld >= 0) meldOf[view] = pair.Value.Meld;
                scene.Tiles.SetOkeyMark(view.gameObject, IsOkey(pair.Key));
                scene.Tiles.SetLying(view.gameObject, !pair.Value.Rack);
                bool slide = pair.Value.Rack && view.HasTarget && IsOnMyRack(view);
                float forced = forcedDurations.TryGetValue(pair.Key, out var d) ? d : 0;
                // Landing effects (spec/12 §1): pile → gold ring, meld → green ring, my rack from outside → "new" bar.
                string fx = slide ? null : pair.Value.Rack ? "mine" : pair.Value.Meld >= 0 ? "meld" : pair.Key == state.Indicator ? null : "disc";
                if (pair.Value.Rack && view.Bar == null) view.Bar = scene.Tiles.Bar(view.gameObject);
                if (instant) view.Snap(pair.Value.Pose); else view.MoveTo(pair.Value.Pose, slide ? TileView.Motion.Slide : TileView.Motion.Travel, 0, forced, fx);
                view.name = pair.Value.Rack ? "tas_" + view.Code + "_rack" : "tas_" + view.Code;
            }

            for (int k = 1; k < 4; k++)
            {
                while (hidden[k].Count > state.HandCounts[k]) { var extra = hidden[k][hidden[k].Count - 1]; hidden[k].RemoveAt(hidden[k].Count - 1); Kill(extra); }
                while (hidden[k].Count < state.HandCounts[k])
                {
                    // Prefer a tile I just saw leave a pile (this player took the previous discard).
                    TileView view = leftovers.FirstOrDefault();
                    if (view != null) leftovers.Remove(view); else view = Spawn(null, deckTop);
                    view.Id = -1; view.Code = null; scene.Tiles.SetFace(view.gameObject, null); scene.Tiles.SetOkeyMark(view.gameObject, false);
                    view.Seat = k; view.OpponentSlot = FreeOpponentSlot(k); hidden[k].Add(view);
                    var pose = scene.SlotPose(k, view.OpponentSlot.Row, view.OpponentSlot.Index);
                    if (instant) view.Snap(pose); else view.MoveTo(pose, TileView.Motion.Travel);
                }
            }
            foreach (var v in leftovers) Kill(v);
            spawnHint = null; lastStock = state.Stock; forcedDurations.Clear();
            if (deckDuration <= 0) LayoutDeck(state.Stock);
        }

        private bool IsOnMyRack(TileView view)
        {
            var rack = scene.RackRoot(0);
            var local = rack.InverseTransformPoint(view.Target.position);
            return Mathf.Abs(local.x) < 0.5f && local.y > -0.06f && local.y < 0.25f && Mathf.Abs(local.z) < 0.08f;
        }

        /// <summary>Green bars under my rack tiles that form valid sets ("Geçerli perleri vurgula").</summary>
        /// <summary>Tile style change: every tile object (rack, table, opponents, deck) takes the scene's new factory look.</summary>
        public void Restyle()
        {
            foreach (var v in known.Values) if (v != null) { scene.Tiles.Restyle(v.gameObject); v.RefreshVisibility(); }
            foreach (var list in hidden) foreach (var v in list) if (v != null) { scene.Tiles.Restyle(v.gameObject); v.RefreshVisibility(); }
            foreach (var go in deckPool) if (go != null) scene.Tiles.Restyle(go);
        }

        /// <summary>Rack-only source badges; also clears markers on tiles that moved to a pile or meld.</summary>
        public void SetTags(System.Func<int, string> tagOf)
        {
            foreach (var pair in known)
            {
                scene.Tiles.SetTag(pair.Value.gameObject, tagOf(pair.Key));
                pair.Value.RefreshVisibility();
            }
        }

        public void SetValid(ICollection<int> valid)
        {
            foreach (var pair in known) pair.Value.Valid = valid != null && valid.Contains(pair.Key);
        }

        // ---------- rack preview (drag) ----------
        public void PreviewRack(RackGrid rack, IReadOnlyDictionary<int, RackSlot> plan, ICollection<TileView> exclude)
        {
            foreach (var id in rack.Tiles)
            {
                var v = ViewOf(id); if (v == null || exclude != null && exclude.Contains(v)) continue;
                if (plan == null || !plan.TryGetValue(id, out var slot) || rack.TryFind(id, out var now) && now.Equals(slot)) { v.Preview(null); continue; }
                v.Preview(scene.SlotPose(0, slot.Row, slot.Index));
            }
        }

        public void ClearPreview(RackGrid rack) => PreviewRack(rack, null, null);

        public Pose SlotPose(RackSlot slot) => scene.SlotPose(0, slot.Row, slot.Index);
    }
}

namespace Galaxy101.Table
{
    /// <summary>
    /// The shared centre board (spec/23, layoutBoard): 8 rows × 20 columns. Each player gets two rows in the order they
    /// opened. Runs sit on their numbers (n → column n−1) and keep one free column at each end for laying off; sets and
    /// pairs take 4 / 2 columns from column 14 on. Melds never overlap and keep their place; a meld that grows into a
    /// neighbour moves alone — to its owner's other row, an empty row, then any row.
    /// </summary>
    public sealed class BoardLayout
    {
        public const int Rows = 8, Columns = 20;
        private readonly List<int> order = new List<int>();
        private readonly Dictionary<int, (int Owner, int Row, int Column, int Length)> placed = new Dictionary<int, (int, int, int, int)>();

        public int Count => placed.Count;
        public IReadOnlyList<int> OpeningOrder => order;
        public bool TryGet(int meld, out (int Owner, int Row, int Column, int Length) at) => placed.TryGetValue(meld, out at);

        public void Reset() { order.Clear(); placed.Clear(); }

        /// <summary>Forget melds no longer on the table (undo, new hand).</summary>
        public void Retain(IEnumerable<int> melds)
        {
            var keep = new HashSet<int>(melds);
            foreach (var id in placed.Keys.Where(k => !keep.Contains(k)).ToList()) placed.Remove(id);
            if (placed.Count == 0) order.Clear();
        }

        public (int Row, int Column) Place(int meld, int owner, bool run, int start, int length)
        {
            if (!order.Contains(owner)) order.Add(owner);
            length = Mathf.Clamp(length, 1, Columns);
            int wanted = run ? Mathf.Clamp(start - 1, 0, Columns - length) : -1;
            if (placed.TryGetValue(meld, out var at))
            {
                // Same place if it still fits (a run growing to the left moves its first column).
                int column = run ? wanted : at.Column;
                if (column + length > Columns) column = Columns - length;
                if (Free(at.Row, column, length, meld)) { placed[meld] = (owner, at.Row, column, length); return (at.Row, column); }
            }
            int k = order.IndexOf(owner);
            var rows = new List<int>();
            if (k < 4) { rows.Add(k * 2); rows.Add(k * 2 + 1); }
            for (int r = 0; r < Rows; r++) if (!rows.Contains(r) && !placed.Values.Any(p => p.Row == r)) rows.Add(r);
            for (int r = 0; r < Rows; r++) if (!rows.Contains(r)) rows.Add(r);
            // Column first, then row: runs keep their numbers, sets keep to column 14+, both in the owner's rows first.
            foreach (int column in Candidates(run, wanted, length))
                foreach (int r in rows)
                    if (Free(r, column, length, meld)) { placed[meld] = (owner, r, column, length); return (r, column); }
            // Board full: overlap in the owner's first row rather than lose the meld.
            int fallback = rows[0]; int c = Mathf.Max(0, wanted);
            placed[meld] = (owner, fallback, c, length); return (fallback, c);
        }

        private static IEnumerable<int> Candidates(bool run, int wanted, int length)
        {
            if (run) { yield return wanted; for (int d = 1; d < Columns; d++) { if (wanted + d + length <= Columns) yield return wanted + d; if (wanted - d >= 0) yield return wanted - d; } yield break; }
            for (int c = 14; c + length <= Columns; c++) yield return c;
            for (int c = 0; c + length <= Columns && c < 14; c++) yield return c;
        }

        /// <summary>Free when no other meld in the row is within one column (the laying-off margin).</summary>
        private bool Free(int row, int column, int length, int self)
        {
            if (column < 0 || column + length > Columns) return false;
            foreach (var pair in placed)
            {
                if (pair.Key == self || pair.Value.Row != row) continue;
                int a0 = column - 1, a1 = column + length, b0 = pair.Value.Column, b1 = pair.Value.Column + pair.Value.Length - 1;
                if (a0 <= b1 && b0 <= a1) return false;
            }
            return true;
        }
    }
}
