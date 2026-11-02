using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Galaxy101.Table
{
    public sealed partial class TableHud
    {
        private enum FlatRole { Rack, Meld, Pile, Indicator, Finish }
        private sealed class FlatTile
        {
            public int Id, Seat, Meld = -1, PaintOrder, CreationOrder;
            public FlatRole Role;
            public VisualElement Element, Ring, Tag, OkeyFace;
            public Image Face;
            public Label OkeyStar, OkeyName;
            public Table2DLayout.Box Box, Placed;
            public bool Positioned;
        }
        private sealed class FlatDrag
        {
            public int Pointer, Id = -1;
            public bool Deck, Side, Moved;
            public Vector2 Start, Position;
            public float HeldAt;
            public List<int> Group;
        }

        private VisualElement flatRoot, flatTilesLayer, flatMarks, flatFelt, flatRack, flatDeck, flatDiscard, flatSide;
        private readonly Dictionary<int, FlatTile> flatTiles = new Dictionary<int, FlatTile>();
        private readonly Dictionary<int, Table2DLayout.Box> flatPositions = new Dictionary<int, Table2DLayout.Box>();
        private TableGame flatGame;
        private FlatDrag flatDrag;
        private int flatSelected = -1, flatLastClick = -1, flatCreationOrder;
        private float flatLastClickTime;
        private string flatLastPerspective = "seat", flatSkinSignature;
        private bool flatApplied, flatCameraEnabled;
        private Texture2D flatAtlas;
        private VisualElement flatGhost;
        private Label flatDeckCount, flatIndicatorLabel, flatOkeyLabel;
        private VisualElement flatOkey;
        private Image flatOkeyFace;
        private readonly VisualElement[] flatHiddenHands = new VisualElement[4];

        /// <summary>A real flat table; the same TableGame owns rules, network requests and the rack arrangement.</summary>
        public bool IsTwoDimensional => Settings?.View == "2d";

        public void ToggleTwoDimensional()
        {
            if (Settings == null) return;
            Settings.Set("view", IsTwoDimensional ? flatLastPerspective : "2d");
        }

        private void InitializeTwoDimensional()
        {
            flatRoot = Layer("table-flat"); flatRoot.pickingMode = PickingMode.Position;
            flatRoot.style.display = DisplayStyle.None; canvas.Add(flatRoot);
            flatFelt = FlatBox("table-flat-felt", new Table2DLayout.Box(28, 64, 1224, 640), "var(--pn)", 46); flatRoot.Add(flatFelt);
            var seam = FlatBox("table-flat-seam", new Table2DLayout.Box(42, 78, 1196, 612), "transparent", 36); Border(seam, 1.5f, new Color(1, 1, 1, .08f)); flatRoot.Add(seam);
            var orbit = new CssDashedCircle(540, 1.5f, new Color(1, 1, 1, .12f)); MenuView.Place(orbit, 370, 60, 540, 540); orbit.pickingMode = PickingMode.Ignore; flatRoot.Add(orbit);
            flatRack = FlatBox("table-flat-rack", new Table2DLayout.Box(222, 494, 836, 168), "linear-gradient(var(--pnH),var(--sd))", 20); flatRoot.Add(flatRack);
            for (int r = 0; r < 2; r++) for (int i = 0; i < 15; i++)
            {
                var slot = Table2DLayout.RackTile(new RackSlot(r, i));
                var well = FlatBox("flat-slot-" + r + "-" + i, slot, "rgba(0,0,0,.22)", 9); flatRoot.Add(well);
            }
            flatDiscard = FlatZone("table-flat-discard", Table2DLayout.Discard, L("TAŞ AT", "DISCARD"));
            flatSide = FlatZone("table-flat-side", Table2DLayout.Side, L("YANDAN AL", "TAKE LEFT"));
            flatDeck = FlatBox("table-flat-deck", Table2DLayout.Deck, "linear-gradient(160deg,#2a3486,#141a4c)", 10);
            flatDeck.pickingMode = PickingMode.Position; flatDeck.style.alignItems = Align.Center; flatDeck.style.justifyContent = Justify.Center;
            flatDeck.Add(Text("101", "Montserrat-800", 15, "var(--a1)")); flatRoot.Add(flatDeck);
            flatDeckCount = Text("", "Baloo2-800", 10, "var(--ic)"); flatDeckCount.style.unityTextAlign = TextAnchor.MiddleCenter; MenuView.Place(flatDeckCount, 593, 332, 94, 18); flatRoot.Add(flatDeckCount);
            flatIndicatorLabel = Text("", "Baloo2-800", 10, "var(--ic)"); flatIndicatorLabel.style.unityTextAlign = TextAnchor.MiddleCenter; MenuView.Place(flatIndicatorLabel, 518, 332, 76, 18); flatRoot.Add(flatIndicatorLabel);
            flatOkey = FlatBox("table-flat-okey", new Table2DLayout.Box(702, 258, 46, 64), "linear-gradient(var(--tf1),var(--tf2))", 8); Border(flatOkey, 2.5f, view.C("var(--a1)")); flatRoot.Add(flatOkey);
            flatOkeyFace = new Image { pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.StretchToFill }; flatOkeyFace.style.position = Position.Absolute; flatOkeyFace.style.left = flatOkeyFace.style.right = flatOkeyFace.style.top = flatOkeyFace.style.bottom = 0; flatOkey.Add(flatOkeyFace);
            flatOkeyLabel = Text("OKEY", "Baloo2-800", 10, "var(--a1)"); flatOkeyLabel.style.unityTextAlign = TextAnchor.MiddleCenter; MenuView.Place(flatOkeyLabel, 690, 332, 70, 18); flatRoot.Add(flatOkeyLabel);
            for (int k = 1; k < 4; k++)
            {
                var backs = new VisualElement { name = "table-flat-hidden-" + k, pickingMode = PickingMode.Ignore }; MenuView.Place(backs, k == 1 ? 1126 : k == 2 ? 530 : 30, k == 2 ? 78 : 406, 220, 20); flatRoot.Add(backs); flatHiddenHands[k] = backs;
            }
            flatTilesLayer = Layer("table-flat-tiles"); flatRoot.Add(flatTilesLayer);
            flatMarks = Layer("table-flat-targets"); flatRoot.Add(flatMarks);
            flatRoot.RegisterCallback<PointerDownEvent>(FlatDown);
            flatRoot.RegisterCallback<PointerMoveEvent>(FlatMove);
            flatRoot.RegisterCallback<PointerUpEvent>(FlatUp);
            flatRoot.RegisterCallback<PointerCancelEvent>(_ => CancelFlatDrag());
            flatRoot.RegisterCallback<PointerCaptureOutEvent>(_ => { if (flatDrag != null) CancelFlatDrag(); });
            flatRoot.RegisterCallback<DetachFromPanelEvent>(evt => { if (evt.target == flatRoot) CancelFlatDrag(); });
        }

        private VisualElement FlatBox(string name, Table2DLayout.Box box, string paint, int radius)
        {
            var element = new VisualElement { name = name, pickingMode = PickingMode.Ignore };
            MenuView.Place(element, box.X, box.Y, box.Width, box.Height); view.Decorate(element, paint, radius); return element;
        }

        private VisualElement FlatZone(string name, Table2DLayout.Box box, string title)
        {
            var zone = FlatBox(name, box, "transparent", 13); Border(zone, 2, new Color(1, 1, 1, .25f)); flatRoot.Add(zone);
            var label = Text(title, "Baloo2-800", 9, "var(--ic)"); label.name = name + "-label"; label.style.unityTextAlign = TextAnchor.MiddleCenter;
            MenuView.Place(label, box.X + box.Width / 2 - 51, box.Y + box.Height + 4, 102, 16); flatRoot.Add(label); return zone;
        }

        private void InitializeFlatSettings()
        {
            Settings.Changed += OnFlatSettingChanged;
            OnFlatSettingChanged(null);
        }

        private void OnFlatSettingChanged(string key)
        {
            if (key != null && key != "view" && key != "tableSkin" && key != "rackSkin" && key != "tileSkin" && key != "lang") return;
            bool active = IsTwoDimensional;
            if (!active) flatLastPerspective = Settings.View;
            if (flatApplied != active)
            {
                ClearFlatEffects();
                CancelFlatDrag(); flatSelected = -1;
                var cam = table.CameraRig.Camera;
                if (active) { flatCameraEnabled = cam.enabled; cam.enabled = false; }
                else { cam.enabled = flatCameraEnabled; flatGame?.SyncBoard(true); }
                flatApplied = active;
            }
            flatRoot.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            view3d.style.display = active ? DisplayStyle.None : DisplayStyle.Flex;
            var vignette = canvas.Q<VisualElement>("table-vignette"); if (vignette != null) vignette.style.display = active ? DisplayStyle.None : DisplayStyle.Flex;
            if (active) RenderFlatTable();
            Render(Current);
        }

        /// <summary>Resynchronise native flat tiles after a board update; diagnostics can request an instantaneous pose.</summary>
        public void SyncFlatTable(bool instant = false) => RenderFlatTable(instant);

        private void RenderFlatTable(bool instant = false)
        {
            if (!IsTwoDimensional || flatRoot == null) return;
            if (flatGame == null) flatGame = FindAnyObjectByType<TableGame>();
            ObserveFlatEffects();
            string skin = table.Planet + "|" + Settings.TableSkin + "|" + Settings.RackSkin + "|" + Settings.TileSkin;
            if (flatSkinSignature != skin)
            {
                flatSkinSignature = skin;
                flatAtlas = Resources.Load<Texture2D>("Galaxy101/Table/TileFaces/TileFaces_" + Settings.TileSkin);
                if (flatAtlas == null) throw new InvalidOperationException("Missing 2D tile face atlas for " + Settings.TileSkin);
                string felt = Settings.TableSkin == "andromeda" ? "andromeda" : Settings.TableSkin + "_" + (ThemeIds.TryGetValue(table.Planet, out var planet) ? planet : "saturn");
                var texture = Resources.Load<Texture2D>("Galaxy101/Table/Felt/" + felt);
                if (texture != null) flatFelt.style.backgroundImage = new StyleBackground(texture);
                string rack = Settings.RackSkin == "walnut" ? "linear-gradient(#643d25,#321b13)" : Settings.RackSkin == "sedef" ? "linear-gradient(#63403e,#241317)" : Settings.RackSkin == "chrome" ? "linear-gradient(#8f9da4,#313b44)" : Settings.RackSkin == "carbon" ? "linear-gradient(#26333e,#101820)" : "linear-gradient(var(--pnH),var(--sd))";
                view.Decorate(flatRack, rack, 20);
            }
            Border(flatDeck, flatGame?.CanDraw == true ? 3 : 2, view.C(flatGame?.CanDraw == true ? "var(--a1)" : "rgba(255,255,255,.12)"));
            flatDeck.style.opacity = Current.DeckCount > 0 ? 1 : .35f;
            flatDeckCount.text = L("DESTE ", "DECK ") + Current.DeckCount; flatIndicatorLabel.text = L("GÖSTERGE", "INDICATOR");
            var sideLabel = flatRoot.Q<Label>("table-flat-side-label"); if (sideLabel != null) sideLabel.text = L("YANDAN AL", "TAKE LEFT");
            var discardLabel = flatRoot.Q<Label>("table-flat-discard-label"); if (discardLabel != null) discardLabel.text = L("TAŞ AT", "DISCARD");
            Border(flatDiscard, 2, view.C(flatGame?.CanDiscard == true ? "var(--a1)" : "rgba(255,255,255,.25)"));
            Border(flatSide, 2, view.C(flatGame?.CanDrawSide == true || flatGame?.PickedTile >= 0 ? "var(--a1)" : "rgba(255,255,255,.25)"));
            var seen = new HashSet<int>(); flatPositions.Clear();
            var game = flatGame; var client = game?.Model;
            for (int k = 1; k < 4; k++)
            {
                int count = Math.Max(0, Math.Min(30, Current.TileCounts[k])); var backs = flatHiddenHands[k];
                if (backs.childCount != count)
                {
                    backs.Clear();
                    for (int n = 0; n < count; n++) backs.Add(FlatBox("hidden-tile-" + k + "-" + n, new Table2DLayout.Box(n * (k == 2 ? 8 : 4), 0, 12, 18), "linear-gradient(#2a3486,#141a4c)", 2));
                }
            }
            bool showOkey = client?.OkeyKnown == true && Current.ShowIndicator;
            flatOkey.style.display = flatOkeyLabel.style.display = showOkey ? DisplayStyle.Flex : DisplayStyle.None;
            flatIndicatorLabel.style.display = game?.Indicator != null && Current.ShowIndicator ? DisplayStyle.Flex : DisplayStyle.None;
            if (showOkey)
            {
                int cell = TileFactory.CellOf(TileIds.Code(client.OkeyColor, client.OkeyNumber)); flatOkeyFace.image = flatAtlas;
                flatOkeyFace.uv = new Rect((cell % 8) / 8f, 1 - (cell / 8 + 1) / 7f, 1 / 8f, 1 / 7f);
            }
            if (client != null && client.HasHand)
            {
                if (client.Seat >= 0)
                    foreach (int id in game.Rack.Tiles)
                    {
                        if (!game.Rack.TryFind(id, out var slot)) continue;
                        ShowFlatTile(id, Table2DLayout.RackTile(slot), FlatRole.Rack, 0, -1, seen, instant);
                    }
                var melds = client.Melds.Select(m => new Table2DLayout.Meld(TileIds.MeldInt(m.Id), game.LocalSeat(m.Owner), TileIds.ToInts(m.Tiles))).ToList();
                var poses = Table2DLayout.MeldTiles(melds);
                foreach (var meld in melds) foreach (int id in meld.Tiles) ShowFlatTile(id, poses[id], FlatRole.Meld, meld.Seat, meld.Id, seen, instant);
                for (int seat = 0; seat < 4; seat++)
                {
                    var pile = client.Discards[game.Abs(seat)];
                    for (int i = Math.Max(0, pile.Count - 5); i < pile.Count; i++) ShowFlatTile(TileIds.ToInt(pile[i]), Table2DLayout.PileTile(seat, i), FlatRole.Pile, seat, -1, seen, instant, i);
                }
                if (game.Indicator != null && Current.ShowIndicator) ShowFlatTile(TileIds.ToInt(game.Indicator), new Table2DLayout.Box(528, 256, 48, 68), FlatRole.Indicator, -1, -1, seen, instant);
                if (game.LastFinishedTile >= 0) ShowFlatTile(game.LastFinishedTile, new Table2DLayout.Box(604, 240, 72, 100), FlatRole.Finish, -1, -1, seen, instant);
                if (flatSelected >= 0 && !game.Rack.TryFind(flatSelected, out _)) { flatSelected = -1; game.SetSelected(-1); }
            }
            foreach (int id in flatTiles.Keys.Where(id => !seen.Contains(id)).ToArray()) { CancelFlatTileMotion(flatTiles[id].Element); flatTiles[id].Element.RemoveFromHierarchy(); flatTiles.Remove(id); }
            ApplyFlatPaintOrder();
            RenderFlatTargets();
        }

        private void ApplyFlatPaintOrder()
        {
            // table2d.js place(): rack 6, meld 5, indicator 4, pile 3 + index, finish 40; follow(): dragged 60.
            int Priority(FlatTile tile) => flatDrag?.Moved == true && (flatDrag.Id == tile.Id || flatDrag.Group?.Contains(tile.Id) == true) ? 60 : tile.PaintOrder;
            var ordered = flatTiles.Values.OrderBy(Priority).ThenBy(tile => tile.CreationOrder).ToArray();
            for (int i = 0; i < ordered.Length; i++)
                if (flatTilesLayer.IndexOf(ordered[i].Element) != i) flatTilesLayer.Insert(i, ordered[i].Element);
        }

        private void ShowFlatTile(int id, Table2DLayout.Box box, FlatRole role, int seat, int meld, HashSet<int> seen, bool instant, int pileIndex = 0)
        {
            if (id < 0) return;
            seen.Add(id); flatPositions[id] = box;
            if (!flatTiles.TryGetValue(id, out var tile))
            {
                var element = new VisualElement { name = "flat-tile-" + id, pickingMode = PickingMode.Position, focusable = true };
                var face = new Image { pickingMode = PickingMode.Ignore, scaleMode = ScaleMode.StretchToFill };
                face.style.position = Position.Absolute; face.style.left = face.style.right = face.style.top = face.style.bottom = 0; element.Add(face);
                var okeyFace = Layer("flat-okey-face"); okeyFace.style.alignItems = Align.Center; okeyFace.style.justifyContent = Justify.Center;
                var okeyStar = Text("★", "Montserrat-900", 29, "#E0A021"); var okeyName = Text("OKEY", "Baloo2-800", 9, "#127A4A"); okeyName.style.marginTop = 2; okeyName.style.letterSpacing = 10;
                okeyFace.Add(okeyStar); okeyFace.Add(okeyName); element.Add(okeyFace);
                var ring = Layer("flat-tile-ring"); element.Add(ring);
                var tag = Text("", "Baloo2-800", 11, "#04222E"); tag.name = "flat-tile-tag"; tag.style.unityTextAlign = TextAnchor.MiddleCenter;
                MenuView.Place(tag, 0, -5, 16, 16); tag.style.left = StyleKeyword.Auto; tag.style.right = -5; element.Add(tag);
                tile = new FlatTile { Id = id, Element = element, Face = face, Ring = ring, Tag = tag, OkeyFace = okeyFace, OkeyStar = okeyStar, OkeyName = okeyName, CreationOrder = flatCreationOrder++ }; element.userData = tile;
                element.RegisterCallback<NavigationSubmitEvent>(evt => { if (tile.Role == FlatRole.Rack) { SelectFlatTile(tile.Id); evt.StopPropagation(); } });
                flatTiles[id] = tile; flatTilesLayer.Add(element);
            }
            tile.Role = role; tile.Seat = seat; tile.Meld = meld; tile.Box = box;
            tile.PaintOrder = role == FlatRole.Rack ? 6 : role == FlatRole.Meld ? 5 : role == FlatRole.Indicator ? 4 : role == FlatRole.Finish ? 40 : 3 + pileIndex;
            tile.Element.style.visibility = id == flatFinishingId && (role != FlatRole.Finish || flatFinishGhost != null) ? Visibility.Hidden : Visibility.Visible;
            int cell = TileFactory.CellOf(TileCodes.Code(id)); tile.Face.image = flatAtlas;
            tile.Face.uv = new Rect((cell % 8) / 8f, 1 - (cell / 8 + 1) / 7f, 1 / 8f, 1 / 7f);
            string body = Settings.TileSkin == "gold" ? "linear-gradient(#181923,#05060c)" : Settings.TileSkin == "neon" ? "linear-gradient(#132039,#080c16)" : "linear-gradient(var(--tf1),var(--tf2))";
            view.Decorate(tile.Element, body, Math.Max(3, (int)(box.Width * .17f)));
            bool realOkey = role != FlatRole.Indicator && flatGame?.Model?.IsOkey(TileIds.ToV4(id)) == true;
            tile.Face.style.display = realOkey ? DisplayStyle.None : DisplayStyle.Flex;
            tile.OkeyFace.style.display = realOkey ? DisplayStyle.Flex : DisplayStyle.None;
            tile.OkeyStar.style.fontSize = Mathf.RoundToInt(box.Height * .42f); tile.OkeyName.style.fontSize = Mathf.Max(7, Mathf.RoundToInt(box.Height * .13f));
            tile.Element.tooltip = realOkey ? "OKEY" : TileCodes.Code(id);
            bool selected = id == flatSelected && role == FlatRole.Rack;
            Border(tile.Ring, selected ? 3 : 0, selected ? view.C("var(--a1)") : Color.clear);
            tile.Tag.style.display = DisplayStyle.None;
            if (role == FlatRole.Rack && flatGame != null)
            {
                bool picked = id == flatGame.PickedTile;
                bool islek = flatGame.Model.OkeyKnown && !flatGame.Model.IsOkey(TileIds.ToV4(id)) && flatGame.Model.IsIslek(TileIds.ToV4(id));
                if (picked || islek) { ((Label)tile.Tag).text = picked ? "←" : "+"; view.Decorate(tile.Tag, picked ? "#FFB547" : "#43D6FF", 8); tile.Tag.style.display = DisplayStyle.Flex; }
            }
            bool dragging = flatDrag?.Moved == true && (flatDrag.Id == id || flatDrag.Group?.Contains(id) == true);
            if (!dragging) PlaceFlatTile(tile, new Table2DLayout.Box(box.X, box.Y - (selected ? 10 : 0), box.Width, box.Height), instant);
        }

        private void RenderFlatTargets()
        {
            flatMarks.Clear(); if (flatGame == null) return;
            int focused = flatDrag?.Moved == true ? flatDrag.Id : flatSelected;
            if (focused < 0 || flatDrag?.Group != null) return;
            flatGame.SetSelected(focused);
            foreach (var target in flatGame.Targets)
            {
                if (!FlatTargetBox(target, out var box)) continue;
                var mark = FlatBox("flat-target-" + target.Meld + "-" + target.Okey + "-" + target.Prepend, box, "rgba(255,201,74,.08)", 6);
                Border(mark, 2, view.C(target.Okey >= 0 ? "#5CF29A" : "#FFC94A")); flatMarks.Add(mark);
            }
        }

        private bool FlatTargetBox(TableGame.LayTarget target, out Table2DLayout.Box box)
        {
            if (target.Okey >= 0) return flatPositions.TryGetValue(target.Okey, out box);
            box = default;
            var meld = flatGame.Model.Melds.FirstOrDefault(m => TileIds.MeldInt(m.Id) == target.Meld);
            if (meld == null || meld.Tiles.Count == 0) return false;
            int id = TileIds.ToInt(target.Prepend ? meld.Tiles[0] : meld.Tiles[meld.Tiles.Count - 1]);
            if (!flatPositions.TryGetValue(id, out var end)) return false;
            box = new Table2DLayout.Box(end.X + (target.Prepend ? -26 : 26), end.Y, 24, 34); return true;
        }

        private TableGame.LayTarget FlatTarget(Vector2 point)
        {
            if (flatGame == null) return null;
            TableGame.LayTarget nearest = null; float distance = 34;
            foreach (var target in flatGame.Targets)
            {
                if (!FlatTargetBox(target, out var box)) continue;
                float d = Vector2.Distance(point, new Vector2(box.X + box.Width / 2, box.Y + box.Height / 2));
                if (d < distance) { nearest = target; distance = d; }
            }
            return nearest;
        }

        private static FlatTile FlatHit(VisualElement element)
        {
            for (var e = element; e != null; e = e.parent) if (e.userData is FlatTile tile) return tile;
            return null;
        }

        private void FlatDown(PointerDownEvent evt)
        {
            if (evt.button != 0 || flatDrag != null || !IsTwoDimensional || flatGame == null || Window != null || Current.ExitOpen || Current.Result != null || Current.Match != null || Current.NetAttempt > 0 || Current.NetGaveUp) return;
            Vector2 point = flatRoot.WorldToLocal(evt.position); var hit = FlatHit(evt.target as VisualElement);
            var drag = new FlatDrag { Pointer = evt.pointerId, Start = point, Position = point, HeldAt = Time.unscaledTime };
            if (hit?.Role == FlatRole.Rack && flatGame.CanArrange) drag.Id = hit.Id;
            else if (hit?.Role == FlatRole.Pile && hit.Seat == 3 && flatGame.CanDrawSide && flatGame.Model.Discards[flatGame.Abs(3)].LastOrDefault() == TileIds.ToV4(hit.Id)) { drag.Id = hit.Id; drag.Side = true; }
            else if (Table2DLayout.Deck.Contains(point.x, point.y) && flatGame.CanDraw) drag.Deck = true;
            else
            {
                if (flatSelected >= 0 && flatGame.CanDiscard && Table2DLayout.Discard.Contains(point.x, point.y, 26)) { int id = flatSelected; SelectFlatTile(-1); flatGame.Discard(id); }
                else if (flatSelected >= 0 && FlatTarget(point) is TableGame.LayTarget target) { int id = flatSelected; flatGame.UseTarget(id, target); SelectFlatTile(-1); }
                return;
            }
            flatDrag = drag; flatRoot.CapturePointer(evt.pointerId); evt.StopPropagation();
            if (Keyboard.current?.shiftKey.isPressed == true) BeginFlatGroup();
        }

        private void BeginFlatGroup()
        {
            if (flatDrag == null || flatDrag.Deck || flatDrag.Side || flatDrag.Group != null || flatGame == null) return;
            var valid = RackEval.Of(flatGame.Rack, flatGame.Model).ValidTiles;
            var segment = flatGame.Rack.Segments().FirstOrDefault(s => s.Ids.Contains(flatDrag.Id));
            if (segment == null || segment.Ids.Length < 2 || !segment.Ids.All(valid.Contains)) return;
            flatDrag.Group = segment.Ids.ToList(); flatDrag.Moved = true; SelectFlatTile(-1);
        }

        private void FlatMove(PointerMoveEvent evt)
        {
            if (flatDrag == null || evt.pointerId != flatDrag.Pointer) return;
            flatDrag.Position = flatRoot.WorldToLocal(evt.position);
            if (!flatDrag.Moved && Vector2.Distance(flatDrag.Start, flatDrag.Position) > 5) { flatDrag.Moved = true; SelectFlatTile(-1); }
            if (!flatDrag.Moved) return;
            if (flatDrag.Id >= 0 && !flatDrag.Side) flatGame.SetDragTile(flatDrag.Id);
            FollowFlatDrag(); evt.StopPropagation();
        }

        private void FollowFlatDrag()
        {
            var drag = flatDrag; if (drag == null || !drag.Moved) return;
            if (drag.Deck)
            {
                if (flatGhost == null) { flatGhost = FlatBox("flat-draw-ghost", new Table2DLayout.Box(0, 0, 50, 70), "linear-gradient(#2a3486,#141a4c)", 9); flatRoot.Add(flatGhost); }
                flatGhost.style.left = drag.Position.x - 25; flatGhost.style.top = drag.Position.y - 35;
            }
            else if (drag.Group != null && flatGame.Rack.TryFind(drag.Id, out var origin))
            {
                foreach (int id in drag.Group) if (flatTiles.TryGetValue(id, out var tile) && flatGame.Rack.TryFind(id, out var slot))
                { CancelFlatTileMotion(tile.Element); MenuView.Place(tile.Element, drag.Position.x - 25 + (slot.Index - origin.Index) * 54, drag.Position.y - 35, 50, 70); }
            }
            else if (flatTiles.TryGetValue(drag.Id, out var tile)) { CancelFlatTileMotion(tile.Element); MenuView.Place(tile.Element, drag.Position.x - 25, drag.Position.y - 35, 50, 70); }
            ApplyFlatPaintOrder();
            RenderFlatTargets();
        }

        private void FlatUp(PointerUpEvent evt)
        {
            var drag = flatDrag; if (drag == null || evt.button != 0 || evt.pointerId != drag.Pointer) return;
            Vector2 point = flatRoot.WorldToLocal(evt.position);
            var marker = drag.Id >= 0 && drag.Moved && drag.Group == null ? FlatTarget(point) : null;
            flatDrag = null; flatRoot.ReleasePointer(drag.Pointer); flatGhost?.RemoveFromHierarchy(); flatGhost = null;
            flatGame.SetDragTile(-1);
            bool slotFound = Table2DLayout.TrySlot(point.x, point.y, out var slot);
            if (drag.Deck || drag.Side)
            {
                if (!drag.Moved || slotFound) _ = flatGame.Draw(drag.Side, slotFound ? slot : (RackSlot?)null);
            }
            else if (!flatGame.CanArrange || !flatGame.Rack.TryFind(drag.Id, out var from)) { SelectFlatTile(-1); }
            else if (drag.Group != null)
            {
                if (slotFound && !slot.Equals(from)) flatGame.ApplyRackPlan(flatGame.Rack.GroupPlan(drag.Group, drag.Group.IndexOf(drag.Id), slot));
            }
            else if (!drag.Moved)
            {
                float now = Time.unscaledTime;
                if (flatGame.DoubleClickDiscard && drag.Id == flatLastClick && now - flatLastClickTime < .38f && flatGame.CanDiscard)
                { SelectFlatTile(-1); flatLastClick = -1; flatGame.Discard(drag.Id); }
                else { flatLastClick = drag.Id; flatLastClickTime = now; SelectFlatTile(flatSelected == drag.Id ? -1 : drag.Id); }
            }
            else if (flatGame.CanDiscard && Table2DLayout.Discard.Contains(point.x, point.y, 26)) flatGame.Discard(drag.Id);
            else if (drag.Id == flatGame.PickedTile && Table2DLayout.Side.Contains(point.x, point.y, 30)) flatGame.PutBack();
            else if (marker != null) flatGame.UseTarget(drag.Id, marker);
            else if (slotFound && !slot.Equals(from)) flatGame.ApplyRackPlan(flatGame.Rack.SinglePlan(drag.Id, slot));
            else if (!slotFound)
            {
                var meldTile = flatTiles.Values.Where(t => t.Role == FlatRole.Meld && t.Box.Contains(point.x, point.y, 10)).OrderBy(t => Vector2.Distance(point, new Vector2(t.Box.X + 12, t.Box.Y + 17))).FirstOrDefault();
                if (meldTile != null) { if (flatGame.IsTableOkey(meldTile.Id)) flatGame.TakeOkey(drag.Id, meldTile.Id); else flatGame.LayOff(drag.Id, meldTile.Meld); }
            }
            RenderFlatTable(); evt.StopPropagation();
        }

        private void SelectFlatTile(int id) { flatSelected = id; flatGame?.SetSelected(id); RenderFlatTable(); }

        private void CancelFlatDrag()
        {
            var drag = flatDrag; flatDrag = null;
            if (drag != null && flatRoot.HasPointerCapture(drag.Pointer)) flatRoot.ReleasePointer(drag.Pointer);
            flatGhost?.RemoveFromHierarchy(); flatGhost = null; flatGame?.SetDragTile(-1);
            if (drag != null && flatRoot?.panel != null) RenderFlatTable();
        }

        private void OnApplicationFocus(bool focused)
        {
            view?.SetFocused(focused || Application.isBatchMode);
            if (!flatEffectsFocused && (focused || Application.isBatchMode)) flatEffectSkipFrame = true;
            flatEffectsFocused = focused || Application.isBatchMode;
            if (!focused) CancelFlatDrag();
        }

        private void UpdateFlatInteraction()
        {
            if (flatDrag != null && !flatDrag.Moved && Time.unscaledTime - flatDrag.HeldAt >= .36f) { BeginFlatGroup(); FollowFlatDrag(); }
        }

        private bool FlatAnchor(string name, out Vector2 point)
        {
            point = name == "a1" ? new Vector2(1188, 292) : name == "a2" ? new Vector2(640, 116) : name == "a3" ? new Vector2(92, 292) : name == "disc" ? new Vector2(1108, 552) : name == "deck" ? new Vector2(640, 290) : new Vector2(-400, -400);
            return name == "a1" || name == "a2" || name == "a3" || name == "disc" || name == "deck";
        }

        private void BuildFlatActions(State state)
        {
            if (flatGame?.Model?.Seat < 0) return;
            var column = Layer("table-flat-actions"); column.style.top = 396; column.style.bottom = StyleKeyword.Auto; column.style.alignItems = Align.Center;
            var spacer = new VisualElement { pickingMode = PickingMode.Ignore }; spacer.style.height = 28; column.Add(spacer);
            var row = Row(10); row.style.marginTop = 8; row.style.flexWrap = Wrap.Wrap; row.style.maxWidth = 1120; row.style.justifyContent = Justify.Center; column.Add(row);
            void Add(string name, string text, Action action, bool enabled = true, bool primary = false)
            {
                var button = view.Button(text, action, primary ? "primary" : "secondary"); button.name = name; button.style.height = 44; button.style.fontSize = 15; button.style.paddingLeft = button.style.paddingRight = 14; button.style.marginBottom = 6; button.SetEnabled(enabled); row.Add(button);
            }
            if (flatGame?.CanDraw == true) Add("table.t2.draw", L("Taş çek", "Draw") + " · " + state.DeckCount, () => { if (flatGame != null) _ = flatGame.Draw(false, null); }, true, true);
            Add("table.t2.sortRun", L("Seri diz", "Sort runs"), () => Press("sort-run"));
            Add("table.t2.sortPair", L("Çift diz", "Sort pairs"), () => Press("sort-pair"));
            var meter = Row(4); meter.style.height = 44; Pad(meter, 0, 12, 0, 12); view.Decorate(meter, "rgba(5,8,22,.6)", 14);
            meter.Add(Text(state.OpenSum + " / " + state.OpenBar, "Montserrat-700", 18, "var(--a1)")); meter.Add(Text(L("Çift ", "Pairs ") + state.Pairs + "/" + state.PairsBar, "Baloo2-800", 10, "var(--mt)")); row.Add(meter);
            if (!state.OpenedPairs[0]) Add("table.t2.doOpen", state.Opened[0] > 0 ? L("Yere aç", "Lay down") : L("Aç", "Open"), () => Press("open"), state.CanOpen, true);
            if (state.Pairs >= 3 && state.Opened[0] == 0) Add("table.t2.doPair", L("Çift aç", "Open pairs"), () => Press("pairs"), state.CanPair);
            if (state.ShowLayPair) Add("table.t2.layPair", L("Yere çift aç", "Lay pairs"), () => Press("pairs"), state.CanLayPair);
            if (state.CanUndo) Add("table.t2.undo", L("Geri topla", "Undo"), () => Press("undo"));
            if (state.ShowReturn || state.ShowPutBack) Add("table.t2.return", L("Geri bırak", "Return"), () => Press("putback"));
            if (state.ShowIsle) Add("table.t2.isle", L("İşle", "Lay off"), () => Press("isle"), state.CanIsle, true);
            if (state.CanFromHand)
            {
                Add("table.t2.bomb", L("Elden bit", "Finish from hand"), () => Press("fromhand"), true, true);
                BuildFlatFinishChoices(row);
            }
            ApplyGaps(row); hudLayer.Add(column);
        }

        private void AddViewToggle(VisualElement bar)
        {
            var button = Row(8); button.name = "table.viewToggle"; button.focusable = true;
            button.tooltip = IsTwoDimensional ? L("3D masaya geç", "Switch to the 3D table") : L("2D masaya geç", "Switch to the 2D table"); button.style.height = 44; button.style.flexShrink = 0;
            Pad(button, 0, 14, 0, 11); view.Decorate(button, "var(--pnH)", 14, 4, "var(--pnS)"); GalaxyFilters.HoverBrightness(button, 1.25f);
            button.Add(view.Icon("table-view-toggle", 20, "var(--a1)"));
            button.Add(Text(IsTwoDimensional ? "3D" : "2D", "Montserrat-800", 15, "var(--ic)"));
            Tap(button, ToggleTwoDimensional);
            button.RegisterCallback<NavigationSubmitEvent>(evt => { ToggleTwoDimensional(); evt.StopPropagation(); });
            bar.Add(button);
        }

        private void DestroyTwoDimensional()
        {
            if (Settings != null) Settings.Changed -= OnFlatSettingChanged;
            ClearFlatEffects();
            CancelFlatDrag();
            if (flatApplied && table?.CameraRig?.Camera != null) table.CameraRig.Camera.enabled = flatCameraEnabled;
            flatTiles.Clear(); flatPositions.Clear(); flatAtlas = null;
        }
    }
}
