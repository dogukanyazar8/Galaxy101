using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using static Galaxy101.Table.TableConstants;

namespace Galaxy101.Table
{
    /// <summary>
    /// Rack interaction (spec/11; table3d.js onDown/onMove/onUp): screen → canvas → camera ray. Click selects (selected
    /// tile lifts 2.2 cm, hover 0.8 cm), double click (380 ms) or selection + ring click discards; dragging a tile previews
    /// singlePlan live; holding 360 ms (or Shift) on a valid set moves the whole set with groupPlan; the deck and the
    /// previous player's discard can be clicked or dragged onto a slot to draw; a tile dropped near a table meld is
    /// laid off. Pointer presses on HUD buttons stay with the UI.
    /// </summary>
    public sealed class RackInput : MonoBehaviour
    {
        [SerializeField] private TableGame game;
        [SerializeField] private TableScene table;
        [SerializeField] private TableHud hud;

        private enum Kind { Rack, Deck, Side, Okey }

        private sealed class Drag
        {
            public Kind Kind; public TileView View; public int Id = -1; public RackSlot From;
            public Vector2 Start; public bool Moved; public float HoldAt = -1; public bool Group;
            public List<(TileView View, int Offset)> Members; public HashSet<TileView> MemberSet; public int Grabbed;
            public string PreviewKey; public IReadOnlyDictionary<int, RackSlot> Plan; public GameObject Ghost;
        }

        private Drag drag;
        private TileView hover;
        private readonly RaycastHit[] hits = new RaycastHit[64];
        private Vector2 hoverScreen = new Vector2(float.NaN, float.NaN);
        private float hoverAt;
        private int selected = -1, lastClickId = -1;
        private Ray? lastRay;
        private float lastClickAt;

        private void Awake()
        {
            if (game == null) game = FindAnyObjectByType<TableGame>();
            if (table == null) table = FindAnyObjectByType<TableScene>();
            if (hud == null) hud = FindAnyObjectByType<TableHud>();
        }

        private void OnDisable() { Galaxy101.Audio.GalaxyCursor.Release(); Galaxy101.Audio.GalaxyCursor.World = null; }

        private void Update()
        {
            if (game == null) Awake();
            var pointer = Pointer.current;
            if (hud != null && hud.IsTwoDimensional)
            {
                if (drag != null)
                {
                    if (drag.Ghost != null) Destroy(drag.Ghost);
                    if (drag.Members != null) foreach (var member in drag.Members) if (member.View != null) member.View.Release();
                    if (drag.View != null) drag.View.Release();
                    drag = null; if (game != null) { game.Board?.ClearPreview(game.Rack); game.SetDragTile(-1); }
                }
                if (selected >= 0 || hover != null)
                    foreach (int id in game.Rack.Tiles) { var tile = game.Board?.ViewOf(id); if (tile != null) tile.LiftWant = 0; }
                selected = -1; hover = null; return;
            }
            if (pointer == null || game == null || game.Board == null || table.CameraRig == null) return;
            Vector2 screen = pointer.position.ReadValue();
            bool down = pointer.press.wasPressedThisFrame, up = pointer.press.wasReleasedThisFrame;
            bool hasRay = TryRay(screen, out var ray); lastRay = hasRay ? ray : (Ray?)null;
            if (down && (OverUi(screen) || !hasRay)) return;
            if (drag == null && down) OnDown(ray, screen);
            else if (drag != null && up) OnUp(ray, hasRay);
            else OnMove(ray, hasRay, screen);
            UpdateLift();
        }

        // ---------- screen → ray ----------
        /// <summary>Screen (bottom-left px) → panel → the 1280×720 canvas → the 3D view's viewport ray.</summary>
        public bool TryRay(Vector2 screen, out Ray ray)
        {
            ray = default;
            var canvas = hud != null ? hud.Canvas : null;
            if (!float.IsFinite(screen.x) || !float.IsFinite(screen.y)) return false;
            if (canvas?.panel == null) { ray = table.CameraRig.Camera.ScreenPointToRay(screen); return true; }
            var root = canvas.panel.visualTree;
            float scale = root.worldBound.width / Mathf.Max(1, Screen.width);
            var local = canvas.WorldToLocal(new Vector2(screen.x, Screen.height - screen.y) * scale);
            // Before the first layout (scene just loaded, or the screen was just resized) the canvas has no size yet.
            if (!float.IsFinite(local.x) || !float.IsFinite(local.y)) return false;
            if (local.x < 0 || local.y < 0 || local.x > TableHud.Width || local.y > TableHud.Height) return false;
            ray = table.CameraRig.Camera.ViewportPointToRay(new Vector3(local.x / TableHud.Width, 1 - local.y / TableHud.Height, 0));
            return true;
        }

        private bool OverUi(Vector2 screen)
        {
            var canvas = hud != null ? hud.Canvas : null;
            if (canvas?.panel == null) return false;
            var root = canvas.panel.visualTree;
            var picked = canvas.panel.Pick(new Vector2(screen.x, Screen.height - screen.y) * (root.worldBound.width / Mathf.Max(1, Screen.width)));
            for (var e = picked; e != null; e = e.parent) { if (e == canvas) return picked != canvas; }
            return false;
        }

        // ---------- picking ----------
        private (Kind Kind, TileView View)? Pick(Ray ray)
        {
            Physics.SyncTransforms(); // tiles move by transform every frame
            float best = float.MaxValue; (Kind, TileView)? hit = null;
            int count = Physics.RaycastNonAlloc(ray, hits, 10f);
            for (int i = 0; i < count; i++)
            {
                var h = hits[i];
                var view = h.collider.GetComponent<TileView>(); if (view == null || h.distance >= best) continue;
                if (game.CanArrange && view.Id >= 0 && game.Rack.TryFind(view.Id, out _)) { best = h.distance; hit = (Kind.Rack, view); }
                else if (game.CanDrawSide && view == SideTop()) { best = h.distance; hit = (Kind.Side, view); }
                else if (view.Id >= 0 && game.IsTableOkey(view.Id)) { best = h.distance; hit = (Kind.Okey, view); }
            }
            if (game.CanDraw && table.DeckBounds(game.Board.DeckShown).IntersectRay(ray, out float d) && d < best) hit = (Kind.Deck, null);
            return hit;
        }

        private TileView SideTop()
        {
            // R-30: the left player's pile (the table's seat 3).
            var model = game.Model; if (model == null || !model.HasHand) return null;
            var pile = model.Discards[game.Abs(3)];
            return pile.Count > 0 ? game.Board.ViewOf(TileIds.ToInt(pile[pile.Count - 1])) : null;
        }

        // table3d.js inZoneTight / inZone / zoneDrop / returnNear: the felt within 14 cm of my discard ring, or the pointer
        // within 100 canvas px of it on screen; a drop counts unless it lands on a rack slot. The left ring (geri bırak)
        // catches within 22 cm or 160 px.
        private bool InZoneTight(Ray ray) => table.TableHit(ray, out var p) && Vector3.Distance(p, table.DiscardCenter(0)) < 0.14f;
        private bool InZone(Ray ray) => InZoneTight(ray) || ScreenNear(ray, table.DiscardCenter(0), 100);
        private bool ZoneDrop(Ray ray) => InZoneTight(ray) || !SlotUnder(ray).HasValue && InZone(ray);
        private bool ReturnNear(Ray ray) => !SlotUnder(ray).HasValue && (table.TableHit(ray, out var p) && Vector3.Distance(p, table.DiscardCenter(3)) < 0.22f || ScreenNear(ray, table.DiscardCenter(3), 160));
        /// <summary>The pointer within <paramref name="px"/> canvas pixels (1280 × 720) of a world point on screen.</summary>
        private bool ScreenNear(Ray ray, Vector3 world, float px)
        {
            var camera = table.CameraRig.Camera; var target = camera.WorldToViewportPoint(world); var pointer = camera.WorldToViewportPoint(ray.origin + ray.direction);
            if (target.z <= 0) return false;
            return new Vector2((target.x - pointer.x) * TableHud.Width, (target.y - pointer.y) * TableHud.Height).sqrMagnitude < px * px;
        }

        /// <summary>
        /// table3d.js nearTarget(id, px): the lay-off / okey target of the focused tile closest to the pointer on screen, within
        /// <paramref name="px"/> canvas pixels (80 while dragging, 60 for a click with a tile selected); none on a rack slot.
        /// </summary>
        private int NearestTarget(Ray ray, float px)
        {
            if (!game.CanDiscard || SlotUnder(ray).HasValue) return -1;
            var camera = table.CameraRig.Camera; var pointer = camera.WorldToViewportPoint(ray.origin + ray.direction);
            int best = -1; float bestSq = px * px; var targets = game.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                var at = camera.WorldToViewportPoint(targets[i].Pose.position); if (at.z <= 0) continue;
                float d = new Vector2((at.x - pointer.x) * TableHud.Width, (at.y - pointer.y) * TableHud.Height).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = i; }
            }
            return best;
        }

        private static RackSlot? SlotAt(Vector3 l)
        {
            if (Mathf.Abs(l.x) > 0.47f || l.y < -0.05f || l.y > 0.24f) return null;
            return new RackSlot(l.y > 0.087f ? 0 : 1, Mathf.Clamp(Mathf.RoundToInt(l.x / SL + 7), 0, 14));
        }

        private RackSlot? SlotUnder(Ray ray) => table.RackHit(ray, out var l) ? SlotAt(l) : null;

        /// <summary>followPose(): 5 cm in front of the rack inside its band, else 5 cm above the felt, face up.</summary>
        private Pose? FollowPose(Ray ray, int offset)
        {
            if (table.RackHit(ray, out var l) && l.y > -0.07f && l.y < 0.3f && Mathf.Abs(l.x) < 0.56f)
                return table.RackPose(new Vector3(Mathf.Clamp(l.x, -0.43f, 0.43f) + offset * SL, Mathf.Clamp(l.y, 0.04f, 0.135f), 0.05f));
            if (!table.TableHit(ray, out var p)) return null;
            var right = table.RackRoot(0).TransformDirection(ThreeSpace.ToUnity(Vector3.right)); right.y = 0; right.Normalize();
            return new Pose(p + Vector3.up * 0.05f + right * (offset * SL), table.FlatForMe);
        }

        // ---------- events ----------
        private void OnDown(Ray ray, Vector2 screen)
        {
            var hit = Pick(ray);
            if (hit == null)
            {
                if (selected >= 0 && game.CanDiscard && InZone(ray)) { int id = selected; selected = -1; game.Discard(id); return; }
                // A click on a lay-off / okey marker plays the selected tile there.
                if (selected >= 0 && game.Markers != null) { int t = NearestTarget(ray, 60); if (t >= 0) { var marker = game.Targets[t]; int id = selected; selected = -1; game.SetSelected(-1); game.UseTarget(id, marker); } } // read before deselecting empties the targets
                return;
            }
            drag = new Drag { Kind = hit.Value.Kind, View = hit.Value.View, Start = screen };
            if (drag.Kind == Kind.Okey) { drag.Id = drag.View.Id; return; }
            if (drag.Kind == Kind.Rack)
            {
                drag.Id = drag.View.Id; game.Rack.TryFind(drag.Id, out drag.From);
                if (InValidSet(drag.Id))
                {
                    bool shift = Keyboard.current != null && Keyboard.current.shiftKey.isPressed;
                    if (shift) StartGroup(ray); else drag.HoldAt = Time.unscaledTime + 0.36f;
                }
            }
        }

        private bool InValidSet(int id) => RackEval.Of(game.Rack, game.Model).ValidTiles.Contains(id);

        private Vector2? ringScreen;

        private void OnMove(Ray ray, bool hasRay, Vector2 screen)
        {
            if (drag == null)
            {
                // Hover picks again when the pointer moves, else ten times a second (tiles may slide under a still pointer).
                if (screen == hoverScreen && Time.unscaledTime < hoverAt) return;
                hoverScreen = screen; hoverAt = Time.unscaledTime + 0.1f;
                var hit = hasRay ? Pick(ray) : null;
                hover = hit?.Kind == Kind.Okey ? null : hit?.View;   // T33 / T48 removed: no okey badge or long-press tip
                // table3d.js canvas cursor: the deck pointer, a tile to take or move grab, the selected tile's ring pointer.
                Galaxy101.Audio.GalaxyCursor.World = hit?.Kind == Kind.Deck ? "pointer" : hit != null && hit.Value.Kind != Kind.Okey ? "grab"
                    : selected >= 0 && game.CanDiscard && hasRay && InZone(ray) ? "pointer" : null;
                // ringGlow(p.kind === 'side' ? .28 : 0) on pointermove: once the pointer has moved, the ring shows only while
                // the left tile is under it (a pointer that never moved leaves it shown).
                if (ringScreen.HasValue && ringScreen.Value != screen || hit?.Kind == Kind.Side)
                    game.SideRingState = new TableFx.SideRing { Hidden = hit?.Kind != Kind.Side, Hover = hit?.Kind == Kind.Side };
                ringScreen = screen;
                return;
            }
            if (drag.Kind == Kind.Okey) return;
            if (!hasRay) return;
            if (!drag.Moved && Vector2.Distance(screen, drag.Start) > 5)
            {
                drag.Moved = true; selected = -1;
                if (drag.Kind == Kind.Rack) game.SetDragTile(drag.Id);
                if (drag.Kind == Kind.Deck)
                {
                    var top = table.DeckTop(game.Board.DeckShown);
                    drag.Ghost = table.Tiles.Create(null, table.TilesRoot); drag.Ghost.name = "tas_hayalet"; Destroy(drag.Ghost.GetComponent<Collider>());
                    drag.Ghost.transform.SetPositionAndRotation(top.position, top.rotation);
                }
            }
            if (drag.Moved && !drag.Group) drag.HoldAt = -1;
            if (!drag.Group && drag.HoldAt > 0 && Time.unscaledTime >= drag.HoldAt) StartGroup(ray);

            if (drag.Group)
            {
                foreach (var m in drag.Members) { var pose = FollowPose(ray, m.Offset); if (pose.HasValue) m.View.Follow(pose.Value); }
                var slot = SlotUnder(ray); string key = slot?.ToString() ?? "none";
                if (key != drag.PreviewKey)
                {
                    drag.PreviewKey = key;
                    drag.Plan = slot.HasValue ? game.Rack.GroupPlan(drag.Members.Select(m => m.View.Id).ToList(), drag.Grabbed, slot.Value) : null;
                    game.Board.PreviewRack(game.Rack, drag.Plan, drag.MemberSet);
                }
                return;
            }
            if (!drag.Moved) return;
            var follow = FollowPose(ray, 0);
            var ring = new TableFx.SideRing { Hidden = drag.Kind != Kind.Side, Hover = drag.Kind == Kind.Side };
            if (drag.Kind == Kind.Rack)
            {
                // table3d.js onMove: over the discard ring the tile snaps above it and bobs (0.03 m + 4 mm · sin 8t); the
                // taken pile tile snaps above its own ring the same way (0.035 m); near a lay-off target it sits on it.
                bool back = drag.Id == game.PickedTile, inReturn = back && ReturnNear(ray), inZone = !inReturn && game.CanDiscard && ZoneDrop(ray);
                float bob = Mathf.Sin(Time.time * 8) * .004f;
                if (game.Fx != null) { game.Fx.ZoneIn = inZone; game.Fx.ZoneDrag = true; }
                if (inZone) follow = new Pose(table.DiscardCenter(0) + Vector3.up * (.03f + bob), table.FlatForMe);
                else if (inReturn) follow = new Pose(table.DiscardCenter(3) + Vector3.up * (.035f + bob), table.SeatPose(3, 0, 0, 0, true).rotation);
                else if (game.Markers != null && game.Markers.Hot >= 0 && game.Markers.Hot < game.Targets.Count && !SlotUnder(ray).HasValue)
                {
                    var target = game.Targets[game.Markers.Hot];
                    follow = new Pose(target.Pose.position + Vector3.up * (target.Okey >= 0 ? .035f : .022f), target.Pose.rotation);
                }
                if (back) ring = new TableFx.SideRing { Returning = true, Hover = ScreenNear(ray, table.DiscardCenter(3), 95) };
            }
            game.SideRingState = ring;
            // Over the ring, a lay-off target or the left ring the drop is a move ("copy" → drop); else grabbing.
            bool dropping = drag.Kind == Kind.Rack && (ring.Returning && ReturnNear(ray) || game.CanDiscard && ZoneDrop(ray) || game.Markers != null && game.Markers.Hot >= 0);
            Galaxy101.Audio.GalaxyCursor.Hold(dropping ? "drop" : "grabbing");
            if (follow.HasValue)
            {
                if (drag.View != null && drag.Kind != Kind.Deck) drag.View.Follow(follow.Value);
                if (drag.Ghost != null) drag.Ghost.transform.SetPositionAndRotation(
                    Vector3.Lerp(drag.Ghost.transform.position, follow.Value.position, Mathf.Min(1, Time.deltaTime * 22)),
                    Quaternion.Slerp(drag.Ghost.transform.rotation, follow.Value.rotation, Mathf.Min(1, Time.deltaTime * 14)));
            }
            if (drag.Kind == Kind.Rack)
            {
                var slot = SlotUnder(ray);
                string key = slot.HasValue && !slot.Value.Equals(drag.From) ? slot.Value.ToString() : "none";
                if (key != drag.PreviewKey)
                {
                    drag.PreviewKey = key;
                    drag.Plan = key == "none" ? null : game.Rack.SinglePlan(drag.Id, slot.Value);
                    game.Board.PreviewRack(game.Rack, drag.Plan, new[] { drag.View });
                }
            }
        }

        private void StartGroup(Ray ray)
        {
            if (drag == null || drag.Kind != Kind.Rack || drag.Group) return;
            var segment = game.Rack.Segments().FirstOrDefault(s => s.Ids.Contains(drag.Id));
            if (segment == null || segment.Ids.Length < 2) return;
            drag.Group = true; drag.Moved = true; selected = -1;
            drag.Grabbed = System.Array.IndexOf(segment.Ids, drag.Id);
            drag.Members = segment.Ids.Select((id, k) => (game.Board.ViewOf(id), k - drag.Grabbed)).Where(m => m.Item1 != null).ToList();
            drag.MemberSet = new HashSet<TileView>(drag.Members.Select(m => m.View));
            foreach (var m in drag.Members) { var pose = FollowPose(ray, m.Offset); if (pose.HasValue) m.View.Follow(pose.Value); m.View.transform.localScale = Vector3.one * 1.05f; m.View.GroupBar = true; }
            game.Message(TableHud.ToastKind.Info, TableStrings.Get("msg.groupMove", hud != null ? hud.L("tr", "en") : "tr"));
        }

        private void OnUp(Ray ray, bool hasRay)
        {
            var d = drag; drag = null;
            // The dragged tile's targets exist only while it is the focus: take the marker under the drop before letting go.
            TableGame.LayTarget marker = null;
            if (d.Kind == Kind.Rack && d.Moved && !d.Group && hasRay && game.Markers != null)
            { int t = NearestTarget(ray, 80); if (t >= 0) marker = game.Targets[t]; }
            game.SetDragTile(-1); game.SideRingState = new TableFx.SideRing { Hidden = true };
            if (game.Fx != null) game.Fx.ZoneIn = game.Fx.ZoneDrag = false;
            Galaxy101.Audio.GalaxyCursor.Release();
            if (d.Kind == Kind.Okey) return;
            if (d.Group)
            {
                game.Board.ClearPreview(game.Rack);
                var slot = hasRay ? SlotUnder(ray) : null;
                if (slot.HasValue && !slot.Value.Equals(d.From)) game.ApplyRackPlan(game.Rack.GroupPlan(d.Members.Select(m => m.View.Id).ToList(), d.Grabbed, slot.Value));
                foreach (var m in d.Members) m.View.Release();
                return;
            }
            if (!d.Moved)
            {
                if (d.Kind == Kind.Deck) { _ = game.Draw(false, null); return; }
                if (d.Kind == Kind.Side) { _ = game.Draw(true, null); return; }
                float now = Time.unscaledTime;
                if (game.DoubleClickDiscard && lastClickId == d.Id && now - lastClickAt < 0.38f && game.CanDiscard) { selected = -1; lastClickId = -1; game.Discard(d.Id); return; }
                lastClickId = d.Id; lastClickAt = now; selected = selected == d.Id ? -1 : d.Id;
                return;
            }
            var target = hasRay ? SlotUnder(ray) : null;
            if (d.Kind == Kind.Rack)
            {
                game.Board.ClearPreview(game.Rack);
                if (!hasRay) { d.View.Release(); return; }
                // A drop that plays a move leaves the tile where it was let go; the move's sync carries it on (table3d.js
                // keeps the released tile in its drag pose). T39: the taken pile tile on its ring goes back (checked first).
                if (d.Id == game.PickedTile && ReturnNear(ray)) { d.View.Park(); game.PutBack(); return; }
                if (game.CanDiscard && ZoneDrop(ray)) { d.View.Park(); game.Discard(d.Id); return; }
                if (game.Markers != null) game.Markers.Hot = -1;
                // A marker within 6 cm takes the tile (spec/23: snaps on approach).
                if (!target.HasValue && marker != null) { d.View.Park(); game.UseTarget(d.Id, marker); return; }
                var okey = target.HasValue ? null : OkeyNear(ray);
                if (okey != null) { d.View.Park(); game.TakeOkey(d.Id, okey.Id); return; }
                int meld = target.HasValue ? -1 : MeldNear(ray);
                if (meld >= 0) { d.View.Park(); game.LayOff(d.Id, meld); return; }
                d.View.Release();
                if (target.HasValue && !target.Value.Equals(d.From)) game.ApplyRackPlan(game.Rack.SinglePlan(d.Id, target.Value));
                return;
            }
            // Drawing by drag: the new tile starts where the ghost / dragged side tile was released.
            if (d.Ghost != null) { game.Board.SpawnFrom(new Pose(d.Ghost.transform.position, d.Ghost.transform.rotation)); Destroy(d.Ghost); }
            if (d.View != null) d.View.Park();   // the drawn pile tile flies on from the drop to its slot
            _ = game.Draw(d.Kind != Kind.Deck, target);
        }

        /// <summary>An okey on the table within 4 cm of the drop point (closer than any other meld tile counts as aiming at it).</summary>
        private TileView OkeyNear(Ray ray)
        {
            if (!game.CanDiscard || !table.TableHit(ray, out var p)) return null;
            TileView best = null; float distance = 0.04f;
            foreach (var v in game.Board.MeldViews)
            {
                if (v.Id < 0 || !game.IsTableOkey(v.Id)) continue;
                float d = Vector3.Distance(v.transform.position, p);
                if (d < distance) { distance = d; best = v; }
            }
            return best;
        }

        /// <summary>A table meld tile within 7.5 cm of the drop point (only when laying off is possible).</summary>
        private int MeldNear(Ray ray)
        {
            if (!game.CanDiscard || !table.TableHit(ray, out var p)) return -1;
            float best = 0.075f; int meld = -1;
            foreach (var v in game.Board.MeldViews)
            {
                float distance = Vector3.Distance(v.transform.position, p);
                if (distance < best) { best = distance; meld = game.Board.MeldOf(v); }
            }
            return meld;
        }

        /// <summary>Select a rack tile as a click would (the state router's scSel; G15/G16 show its targets).</summary>
        public void Select(int id) { selected = id; drag = null; }

        private void UpdateLift()
        {
            game.SetSelected(drag == null ? selected : -1);
            if (drag != null && drag.Moved && drag.Kind == Kind.Rack && game.Markers != null && lastRay.HasValue) game.Markers.Hot = NearestTarget(lastRay.Value, 80);
            foreach (var id in game.Rack.Tiles)
            {
                var v = game.Board.ViewOf(id); if (v == null) continue;
                v.LiftWant = id == selected ? 0.022f : v == hover && game.CanArrange ? 0.008f : 0;
            }
            if (selected >= 0 && !game.Rack.TryFind(selected, out _)) selected = -1;
        }
    }
}
