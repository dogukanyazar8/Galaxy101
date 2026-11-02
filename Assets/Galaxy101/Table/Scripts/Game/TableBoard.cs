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
        /// <summary>
        /// G101-Masa pushDisc: a pile shows its last <see cref="PileShown"/> tiles; an older one is gone for good (it never
        /// comes back when the top is taken), so the 3D pile never stacks tiles at one height.
        /// </summary>
        public const int PileShown = 4;
        private readonly HashSet<int> buried = new HashSet<int>();

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
            meldOf.Clear(); buried.Clear(); spawnHint = null; Layout.Reset(); indicatorId = -1;
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
            if (deckDuration <= 0) { scene.UpdatePairZone(Layout.HasPairs, Time.deltaTime); return; }
            float u = Mathf.Clamp01((Time.time - deckT0) / deckDuration);
            if (u >= 1) deckDuration = 0;
            // The deal over, the stock settles into its piles (and the indicator onto the six-tile pile).
            LayoutDeck(Mathf.RoundToInt(Mathf.Lerp(deckFrom, deckTo, u)));
            if (deckDuration <= 0 && indicatorId >= 0 && known.TryGetValue(indicatorId, out var settled) && settled != null && !settled.Tweening) settled.Snap(scene.IndicatorPose());
            scene.UpdatePairZone(Layout.HasPairs, Time.deltaTime);
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
            var inPiles = new HashSet<int>();
            for (int k = 0; k < 4; k++)
            {
                var pile = state.Discards[k];
                int shown = 0;
                for (int j = 0; j < pile.Count; j++) { inPiles.Add(pile[j]); if (!buried.Contains(pile[j])) shown++; }
                for (int j = 0; j < pile.Count && shown > PileShown; j++)
                    if (buried.Add(pile[j])) { shown--; if (known.TryGetValue(pile[j], out var gone)) { known.Remove(pile[j]); Kill(gone); } }
                int level = 0;
                foreach (int id in pile)
                    if (!buried.Contains(id)) desired[id] = (scene.DiscardPose(k, level++, id.ToString()), k, false, -1);
            }
            buried.RemoveWhere(id => !inPiles.Contains(id));
            var layoutMelds = new BoardLayout.Meld[state.Melds.Count];
            for (int i = 0; i < state.Melds.Count; i++)
            {
                var meld = state.Melds[i]; var kind = Classify(meld.Tiles);
                layoutMelds[i] = new BoardLayout.Meld(meld.Id, meld.Owner, kind.Run, kind.Start, meld.Tiles.Length, meld.Tiles);
            }
            Layout.Reconcile(layoutMelds);
            foreach (var meld in state.Melds)
            {
                Layout.TryGet(meld.Id, out var at);
                int row = at.Row, column = at.Column;
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
                bool fromRack = false;
                if (!known.TryGetValue(pair.Key, out var view))
                {
                    int seat = pair.Value.Seat;
                    // A newly visible tile from an opponent: it leaves their hidden rack (discard, meld).
                    if (seat > 0 && hidden[seat].Count > state.HandCounts[seat]) { view = hidden[seat][hidden[seat].Count - 1]; hidden[seat].RemoveAt(hidden[seat].Count - 1); fromRack = true; }
                    else view = Spawn(null, deckTop);
                    view.Id = pair.Key; view.Seat = -1; view.Code = TileCodes.Code(pair.Key); scene.Tiles.SetFace(view.gameObject, view.Code);
                    known[pair.Key] = view;
                }
                if (pair.Value.Meld >= 0) meldOf[view] = pair.Value.Meld;
                scene.Tiles.SetOkeyMark(view.gameObject, IsOkey(pair.Key));
                scene.Tiles.SetLying(view.gameObject, !pair.Value.Rack);
                fromRack |= view.HasTarget && IsOnMyRack(view);
                bool slide = pair.Value.Rack && fromRack;
                // table3d.js sync: rack → meld is 'lay' (no arc, no landing effect), as an opponent's opening is.
                bool lay = !pair.Value.Rack && pair.Value.Meld >= 0 && fromRack;
                float forced = forcedDurations.TryGetValue(pair.Key, out var d) ? d : 0;
                // Landing effects (spec/12 §1): pile → gold ring, meld → green ring, my rack from outside → "new" bar.
                string fx = slide || lay ? null : pair.Value.Rack ? "mine" : pair.Value.Meld >= 0 ? "meld" : pair.Key == state.Indicator ? null : "disc";
                if (pair.Value.Rack && view.Bar == null) view.Bar = scene.Tiles.Bar(view.gameObject);
                if (instant) view.Snap(pair.Value.Pose); else view.MoveTo(pair.Value.Pose, slide ? TileView.Motion.Slide : lay ? TileView.Motion.Lay : TileView.Motion.Travel, 0, forced, fx);
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
                    // An opponent's rack from the deck or a pile: 'travel' (gold trail, two small hops, pop).
                    if (instant) view.Snap(pose); else view.MoveTo(pose, TileView.Motion.Travel, 0, 0, "travel");
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
    /// HTML layoutBoard: retained physical-tile anchors are reconciled before new melds. Runs and groups prefer
    /// columns 0–13, pairs 15–19; column 14 divides the zones. New runs reserve their extendable ends and groups
    /// reserve four cells. Existing anchors only require the actual tiles and their one-cell gap to fit.
    /// </summary>
    public sealed class BoardLayout
    {
        public const int Rows = 8, Columns = 20;
        public readonly struct Meld
        {
            public readonly int Id, Owner, Start, Length;
            public readonly bool Run;
            public readonly int[] Tiles;
            public Meld(int id, int owner, bool run, int start, int length, int[] tiles = null)
            { Id = id; Owner = owner; Start = start; Length = Mathf.Clamp(length, 1, Columns); Run = run && Length != 2; Tiles = tiles; }
        }

        /// <summary>Source cloneAnch(): one independent snapshot, retaining aliases between every tile of a meld.</summary>
        public sealed class Snapshot
        {
            private readonly BoardLayout state = new BoardLayout();
            internal Snapshot(BoardLayout source) { state.CopyFrom(source); }
            internal void ApplyTo(BoardLayout target) => target.CopyFrom(state);
        }

        private sealed class Anchor { public int Sequence, Row = -1, X0, Kind; public int? Base; }
        private static int KindOf(Meld meld) => meld.Run ? 0 : meld.Length == 2 ? 2 : 1;
        private sealed class Item
        {
            public Meld Meld; public Anchor Anchor; public int Column;
            public bool Pair => Meld.Length == 2;
            public int Width => Meld.Run ? Meld.Length : Pair ? 2 : Mathf.Max(4, Meld.Length);
        }
        private readonly struct Interval
        {
            public readonly int Start, End, ReserveStart, ReserveEnd;
            public Interval(int start, int end, int reserveStart, int reserveEnd)
            { Start = start; End = end; ReserveStart = reserveStart; ReserveEnd = reserveEnd; }
        }

        private readonly List<int> order = new List<int>();
        private readonly Dictionary<int, (int Owner, int Row, int Column, int Length)> placed = new Dictionary<int, (int, int, int, int)>();
        private readonly Dictionary<int, Anchor> byTile = new Dictionary<int, Anchor>();
        private readonly Dictionary<int, Anchor> byMeld = new Dictionary<int, Anchor>();
        private readonly List<Meld> active = new List<Meld>();
        private int sequence;

        public int Count => placed.Count;
        public IReadOnlyList<int> OpeningOrder => order;
        public bool HasPairs { get { foreach (var meld in active) if (meld.Length == 2) return true; return false; } }
        public bool TryGet(int meld, out (int Owner, int Row, int Column, int Length) at) => placed.TryGetValue(meld, out at);

        public void Reset() { ResetAnchors(); active.Clear(); }
        private void ResetAnchors() { order.Clear(); placed.Clear(); byTile.Clear(); byMeld.Clear(); sequence = 0; }

        public Snapshot Capture() => new Snapshot(this);
        public void Restore(Snapshot snapshot)
        {
            if (snapshot == null) throw new System.ArgumentNullException(nameof(snapshot));
            snapshot.ApplyTo(this);
        }

        private void CopyFrom(BoardLayout source)
        {
            Reset(); order.AddRange(source.order); sequence = source.sequence;
            var copies = new Dictionary<Anchor, Anchor>();
            Anchor Copy(Anchor anchor)
            {
                if (!copies.TryGetValue(anchor, out var clone))
                {
                    clone = new Anchor { Sequence = anchor.Sequence, Row = anchor.Row, X0 = anchor.X0, Kind = anchor.Kind, Base = anchor.Base };
                    copies.Add(anchor, clone);
                }
                return clone;
            }
            foreach (var pair in source.byTile) byTile.Add(pair.Key, Copy(pair.Value));
            foreach (var pair in source.byMeld) byMeld.Add(pair.Key, Copy(pair.Value));
            foreach (var pair in source.placed) placed.Add(pair.Key, pair.Value);
            foreach (var meld in source.active)
                active.Add(new Meld(meld.Id, meld.Owner, meld.Run, meld.Start, meld.Length, meld.Tiles == null ? null : (int[])meld.Tiles.Clone()));
        }

        /// <summary>Forget melds no longer on the table (undo, new hand).</summary>
        public void Retain(IEnumerable<int> melds)
        {
            var keep = new HashSet<int>(melds);
            Reconcile(active.Where(m => keep.Contains(m.Id)).ToArray());
        }

        public (int Row, int Column) Place(int meld, int owner, bool run, int start, int length)
        {
            var next = active.ToList();
            int index = next.FindIndex(m => m.Id == meld);
            var value = new Meld(meld, owner, run, start, length);
            if (index < 0) next.Add(value); else next[index] = value;
            Reconcile(next);
            var at = placed[meld]; return (at.Row, at.Column);
        }

        /// <summary>One complete state, sorted by source owner/group order, with old anchors kept in a separate first pass.</summary>
        public void Reconcile(IReadOnlyList<Meld> melds)
        {
            var next = melds.OrderBy(m => m.Owner).ToArray();
            // Source sync discards the anchor board if no current physical tile belongs to it (new hand / full undo).
            bool retained = next.Any(m => m.Tiles != null ? m.Tiles.Any(byTile.ContainsKey) : byMeld.ContainsKey(m.Id));
            if (!retained) ResetAnchors();
            active.Clear(); active.AddRange(next);
            foreach (var meld in next) if (!order.Contains(meld.Owner)) order.Add(meld.Owner);
            // A single tile that left its meld (an okey taken from a run or group, then laid in a new pair) must not drag
            // the new meld onto its old meld's place: an anchor goes to the meld holding most of its tiles, and only to a
            // meld of the same kind (run / group / pair keep their own zones).
            var claims = new Anchor[next.Length];
            var hits = new int[next.Length];
            for (int i = 0; i < next.Length; i++)
            {
                var meld = next[i];
                if (meld.Tiles == null) { if (byMeld.TryGetValue(meld.Id, out var own) && own.Kind == KindOf(meld)) { claims[i] = own; hits[i] = int.MaxValue; } continue; }
                foreach (int tile in meld.Tiles)
                {
                    if (!byTile.TryGetValue(tile, out var anchor) || anchor.Kind != KindOf(meld)) continue;
                    int count = meld.Tiles.Count(t => byTile.TryGetValue(t, out var a) && a == anchor);
                    if (count > hits[i]) { hits[i] = count; claims[i] = anchor; }
                }
            }
            var items = new List<Item>(next.Length);
            var owner = new Dictionary<Anchor, int>();
            for (int i = 0; i < next.Length; i++)
            {
                if (claims[i] == null) continue;
                if (!owner.TryGetValue(claims[i], out int other) || hits[i] > hits[other]) owner[claims[i]] = i;
            }
            var currentTiles = new HashSet<int>();
            for (int i = 0; i < next.Length; i++)
            {
                var meld = next[i];
                var anchor = claims[i] != null && owner[claims[i]] == i ? claims[i] : new Anchor { Sequence = ++sequence, Kind = KindOf(meld) };
                if (meld.Tiles != null) foreach (int tile in meld.Tiles) { byTile[tile] = anchor; currentTiles.Add(tile); }
                byMeld[meld.Id] = anchor;
                items.Add(new Item { Meld = meld, Anchor = anchor });
            }
            foreach (int tile in byTile.Keys.Where(t => !currentTiles.Contains(t)).ToArray()) byTile.Remove(tile);
            items.Sort((a, b) => a.Anchor.Sequence.CompareTo(b.Anchor.Sequence));
            var occupied = new List<Interval>[Rows];
            for (int row = 0; row < Rows; row++) occupied[row] = new List<Interval>();
            var later = new List<Item>();
            foreach (var item in items)
                if (item.Anchor.Row < 0 || item.Meld.Run && !item.Anchor.Base.HasValue || !Put(item, item.Anchor.Row, item.Anchor.X0, true, 0, 19, occupied)) later.Add(item);
            foreach (var item in later)
            {
                if (item.Meld.Run) item.Anchor.Base = item.Meld.Start;
                var preferences = item.Meld.Run ? new[] { Mathf.Max(0, item.Meld.Start - 1) } : item.Pair ? new[] { 15, 18 } : new[] { 9, 10, 8, 5, 0 };
                var columns = preferences.Concat(Enumerable.Range(0, Columns).Where(x => !preferences.Contains(x))).ToArray();
                var rows = RowsFor(item.Meld.Owner);
                bool found = false;
                for (int zone = 0; zone < 2 && !found; zone++)
                {
                    bool pairZone = item.Pair ? zone == 0 : zone == 1;
                    foreach (int row in rows)
                    {
                        foreach (int column in columns)
                            if (Put(item, row, column, false, pairZone ? 15 : 0, pairZone ? 19 : 13, occupied)) { found = true; break; }
                        if (found) break;
                    }
                }
                if (found) continue;
                // Same source full-board fallback: preserve every tile even when no valid interval remains.
                item.Anchor.Row = rows[0]; item.Anchor.X0 = item.Column = Mathf.Max(0, Columns - item.Width);
                occupied[rows[0]].Add(new Interval(item.Column, item.Column + item.Width - 1, item.Column, item.Column + item.Width - 1));
                Debug.LogWarning("[G101] tahtada yer kalmadı");
            }
            placed.Clear();
            foreach (var item in items) placed[item.Meld.Id] = (item.Meld.Owner, item.Anchor.Row, item.Column, item.Meld.Length);
        }

        private List<int> RowsFor(int owner)
        {
            int k = order.IndexOf(owner);
            var rows = new List<int>();
            if (k < 4) { rows.Add(k * 2); rows.Add(k * 2 + 1); }
            for (int r = 0; r < Rows; r++) if (!rows.Contains(r) && r / 2 >= order.Count) rows.Add(r);
            for (int r = 0; r < Rows; r++) if (!rows.Contains(r)) rows.Add(r);
            return rows;
        }

        private static bool Put(Item item, int row, int x0, bool keep, int zoneStart, int zoneEnd, List<Interval>[] occupied)
        {
            int column = item.Meld.Run ? x0 + item.Meld.Start - item.Anchor.Base.Value : x0;
            int end = column + item.Width - 1;
            var interval = new Interval(column, column + item.Meld.Length - 1,
                item.Meld.Run && item.Meld.Start > 1 ? column - 1 : column,
                item.Meld.Run && item.Meld.Start + item.Meld.Length - 1 < 13 ? end + 1 : end);
            if (interval.Start < zoneStart || interval.End > zoneEnd || !keep && (interval.ReserveStart < zoneStart || interval.ReserveEnd > zoneEnd)) return false;
            foreach (var other in occupied[row])
            {
                bool free = keep ? interval.End < other.Start - 1 || interval.Start > other.End + 1
                    : interval.ReserveEnd < other.ReserveStart - 1 || interval.ReserveStart > other.ReserveEnd + 1;
                if (!free) return false;
            }
            occupied[row].Add(interval); item.Anchor.Row = row; item.Anchor.X0 = x0; item.Column = column; return true;
        }
    }
}
