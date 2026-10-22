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
            var view = go.AddComponent<TileView>(); view.Code = code; return view;
        }

        private void Kill(TileView view) { meldOf.Remove(view); if (view != null) Object.Destroy(view.gameObject); }

        public void Clear()
        {
            foreach (var v in known.Values) Kill(v); known.Clear();
            foreach (var h in hidden) { foreach (var v in h) Kill(v); h.Clear(); }
            meldOf.Clear(); spawnHint = null;
        }

        // ---------- deck ----------
        public void LayoutDeck(int n)
        {
            n = Mathf.Clamp(n, 0, 106); if (n == deckShown) return; deckShown = n;
            while (deckPool.Count < n) { var go = scene.Tiles.Create(null, scene.TilesRoot); go.name = "deste_" + deckPool.Count; Object.Destroy(go.GetComponent<Collider>()); deckPool.Add(go); }
            for (int j = 0; j < deckPool.Count; j++)
            {
                bool on = j < n; deckPool[j].SetActive(on);
                if (on) { var p = scene.DeckPose(j, n); deckPool[j].transform.SetPositionAndRotation(p.position, p.rotation); }
            }
        }

        /// <summary>Per-frame: the deal counts the deck down linearly (106 → stock).</summary>
        public void Tick()
        {
            if (deckDuration <= 0) return;
            float u = Mathf.Clamp01((Time.time - deckT0) / deckDuration);
            LayoutDeck(Mathf.RoundToInt(Mathf.Lerp(deckFrom, deckTo, u)));
            if (u >= 1) deckDuration = 0;
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
                    if (k == 0) { if (mine.Count == 0) continue; int id = mine.Dequeue(); var v = Spawn(TileCodes.Code(id), deckTop); v.Id = id; known[id] = v; scene.Tiles.SetOkeyMark(v.gameObject, IsOkey(id)); v.MoveTo(RackPose(state.Rack, id), TileView.Motion.Deal, lead + index++ * step); }
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
                ind.MoveTo(scene.IndicatorPose(), TileView.Motion.Travel, dealt + 0.5f);
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
            foreach (var group in state.Melds.GroupBy(m => m.Owner))
            {
                int column = 0, row = 0;
                foreach (var meld in group)
                {
                    if (column + meld.Tiles.Length > 11) { column = 0; row++; }
                    foreach (var id in meld.Tiles) desired[id] = (scene.MeldPose(group.Key, column++, row), group.Key, false, meld.Id);
                    column++;
                }
            }
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
                bool slide = pair.Value.Rack && view.HasTarget && IsOnMyRack(view);
                float forced = forcedDurations.TryGetValue(pair.Key, out var d) ? d : 0;
                if (instant) view.Snap(pair.Value.Pose); else view.MoveTo(pair.Value.Pose, slide ? TileView.Motion.Slide : TileView.Motion.Travel, 0, forced);
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
