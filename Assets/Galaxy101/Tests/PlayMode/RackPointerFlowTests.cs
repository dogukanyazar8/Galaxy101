using System;
using System.Collections;
using System.IO;
using System.Linq;
using Galaxy101.Table;
using NUnit.Framework;
using Okey101.Core.V4;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    /// <summary>
    /// The 3D table driven by a real pointer (Input System mouse events through RackInput), as a player plays it
    /// (table3d.js onDown / onMove / onUp): click and double click, select + ring click, drag to the ring, drag the left
    /// tile onto a slot, click the deck. The released tile flies on from where it was let go.
    /// </summary>
    public sealed class RackPointerFlowTests
    {
        private GameObject gameOwner, sceneOwner, hudOwner;
        private string statePath;
        private Mouse mouse;
        private InputSettings.BackgroundBehavior savedBackground;
#if UNITY_EDITOR
        private InputSettings.EditorInputBehaviorInPlayMode savedEditor;
#endif
        private TableGame game;
        private TableScene table;
        private TableHud hud;

        private IEnumerator Build(string flow)
        {
            // Batch-mode play has no focused game view: let a test device's input through regardless.
            savedBackground = InputSystem.settings.backgroundBehavior; InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            savedEditor = InputSystem.settings.editorInputBehaviorInPlayMode; InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            mouse = InputSystem.AddDevice<Mouse>(); mouse.MakeCurrent();
            TableGame.Pending = new TableLaunch
            {
                Setup = new TableSetup { Options = new MatchOptions { Mode = "solo", Hands = 5 }, Seed = 42, TurnMs = 60000 },
                Table = TableGame.DefaultTable(), Flow = flow, Clock = () => Time.timeAsDouble * 1000,
            };
            gameOwner = new GameObject("TableGame"); game = gameOwner.AddComponent<TableGame>();
            sceneOwner = new GameObject("GalaxyTable"); table = sceneOwner.AddComponent<TableScene>();
            statePath = Path.Combine(Path.GetTempPath(), "galaxy-pointer-" + Guid.NewGuid().ToString("N") + ".json");
            hudOwner = new GameObject("TableHud"); hudOwner.SetActive(false);
            hudOwner.AddComponent<UIDocument>().panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            hud = hudOwner.AddComponent<TableHud>(); hudOwner.SetActive(true); hud.Initialize(statePath);
            gameOwner.AddComponent<RackInput>();   // after the HUD: the pointer maps through its canvas
            float end = Time.realtimeSinceStartup + 15;
            while (!(game.HumanTurn && hud.Canvas.layout.width > 0)) { if (Time.realtimeSinceStartup > end) Assert.Fail("My turn did not come."); yield return null; }
            yield return Settle(.6f);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            TableGame.Pending = null;
            foreach (var go in new[] { hudOwner, gameOwner, sceneOwner }) if (go != null) UnityEngine.Object.Destroy(go);
            if (mouse != null) { InputSystem.RemoveDevice(mouse); InputSystem.settings.backgroundBehavior = savedBackground;
#if UNITY_EDITOR
                InputSystem.settings.editorInputBehaviorInPlayMode = savedEditor;
#endif
            }
            yield return null;
            if (statePath != null && File.Exists(statePath)) File.Delete(statePath);
        }

        private static IEnumerator Settle(float seconds) { float end = Time.realtimeSinceStartup + seconds; while (Time.realtimeSinceStartup < end) yield return null; }

        private static IEnumerator Until(Func<bool> condition, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition()) { if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what); yield return null; }
        }

        /// <summary>A world point → the screen pixel the pointer must be at (camera viewport → 1280×720 canvas → panel → screen).</summary>
        private Vector2 ScreenOf(Vector3 world)
        {
            var v = table.CameraRig.Camera.WorldToViewportPoint(world);
            var canvas = hud.Canvas; var panelPoint = canvas.LocalToWorld(new Vector2(v.x * TableHud.Width, (1 - v.y) * TableHud.Height));
            float scale = canvas.panel.visualTree.worldBound.width / Mathf.Max(1, Screen.width);
            return new Vector2(panelPoint.x / scale, Screen.height - panelPoint.y / scale);
        }

        /// <summary>
        /// The ring's visible edge: the action panel covers its centre on this camera, as in G101-Masa, where the ring is hit
        /// within 100 px on screen (inZone). 60 canvas px to its left.
        /// </summary>
        private Vector2 RingScreen()
        {
            var canvas = hud.Canvas; float pxToScreen = canvas.worldBound.width / TableHud.Width / (canvas.panel.visualTree.worldBound.width / Mathf.Max(1, Screen.width));
            return ScreenOf(table.DiscardCenter(0)) + new Vector2(-60 * pxToScreen, 0);
        }

        private IEnumerator Pointer(Vector2 at, bool pressed)
        {
            var state = new MouseState { position = at }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left, pressed);
            InputSystem.QueueStateEvent(mouse, state);
            yield return null; yield return null;   // processed in the next frame's input update, read by RackInput.Update
        }

        private IEnumerator Click(Vector3 world) { var at = ScreenOf(world); yield return Pointer(at, false); yield return Pointer(at, true); yield return Pointer(at, false); }

        private IEnumerator Drag(Vector3 from, Vector3 to, Func<IEnumerator> beforeRelease = null) => Drag(ScreenOf(from), ScreenOf(to), beforeRelease);

        private IEnumerator Drag(Vector2 a, Vector2 b, Func<IEnumerator> beforeRelease = null, bool held = false)
        {
            if (!held) { yield return Pointer(a, false); yield return Pointer(a, true); }
            for (int i = 1; i <= 12; i++) yield return Pointer(Vector2.Lerp(a, b, i / 12f), true);
            float until = Time.realtimeSinceStartup + .5f;   // the dragged tile catches up (lerp dt·22)
            while (Time.realtimeSinceStartup < until) yield return Pointer(b, true);
            if (beforeRelease != null) yield return beforeRelease();
            yield return Pointer(b, false);
        }

        private int LooseTile() => game.Rack.Tiles.First(id => !RackEval.Of(game.Rack, game.Model).ValidTiles.Contains(id));
        private int MyPileCount => game.Model.Discards[game.Abs(0)].Count;

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator DoubleClickDiscards()
        {
            yield return Build("open");
            Assert.That(game.DoubleClickDiscard, Is.True, "G101-Masa SET_DEFAULTS dbl: true");
            int tile = LooseTile(), before = MyPileCount;
            var at = game.Board.ViewOf(tile).transform.position;
            yield return Click(at); yield return Click(at);
            yield return Until(() => MyPileCount == before + 1, 5, "the double-clicked tile on my pile");
            Assert.That(game.Rack.TryFind(tile, out _), Is.False);
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator SelectThenRingClickDiscards()
        {
            yield return Build("open");
            int tile = LooseTile(), before = MyPileCount;
            yield return Click(game.Board.ViewOf(tile).transform.position);
            yield return Settle(.5f);   // a second click within 380 ms would be a double click
            var ring = RingScreen(); yield return Pointer(ring, false); yield return Pointer(ring, true); yield return Pointer(ring, false);
            yield return Until(() => MyPileCount == before + 1, 5, "the selected tile on my pile");
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator DragToTheRingSnapsAboveItAndFliesOnFromThere()
        {
            yield return Build("open");
            int tile = LooseTile(), before = MyPileCount; var view = game.Board.ViewOf(tile); var home = view.Target.position;
            var zone = table.DiscardCenter(0);
            yield return Drag(ScreenOf(view.transform.position), RingScreen(), () =>
            {
                // table3d.js onMove: over the ring the tile sits 3 cm above it (± 4 mm bob).
                Assert.That(Vector2.Distance(new Vector2(view.transform.position.x, view.transform.position.z), new Vector2(zone.x, zone.z)), Is.LessThan(.02f), "snapped above the ring");
                return Settle(0);
            });
            float released = Vector3.Distance(view.transform.position, home);
            for (int i = 0; i < 6; i++) { yield return null; Assert.That(Vector3.Distance(view.transform.position, home), Is.GreaterThan(released * .6f), "no slide back towards the rack before it flies"); }
            yield return Until(() => MyPileCount == before + 1, 5, "the dropped tile on my pile");
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator DraggingTheLeftTileOntoASlotDrawsItThere()
        {
            yield return Build("play");
            var pile = game.Model.Discards[game.Abs(3)]; Assert.That(pile.Count, Is.GreaterThan(0));
            int side = TileIds.ToInt(pile[pile.Count - 1]); var free = game.Rack.FirstFree().Value;
            yield return Drag(game.Board.ViewOf(side).transform.position, table.SlotPose(0, free.Row, free.Index).position);
            yield return Until(() => game.Rack.TryFind(side, out _), 5, "the left tile on my rack");
            game.Rack.TryFind(side, out var at);
            Assert.That(at, Is.EqualTo(free), "dropped on a slot, it takes that slot");
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator ClickingTheDeckDraws()
        {
            yield return Build("play");
            int count = game.Rack.Count, deck = game.Model.Deck;
            yield return Click(table.DeckTop(game.Board.DeckShown).position);
            yield return Until(() => game.Rack.Count == count + 1, 5, "a tile from the deck");
            Assert.That(game.Model.Deck, Is.EqualTo(deck - 1));
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator DraggingTheDeckOntoASlotDrawsIntoIt()
        {
            yield return Build("play");
            var free = game.Rack.FirstFree().Value; int count = game.Rack.Count;
            yield return Drag(table.DeckTop(game.Board.DeckShown).position, table.SlotPose(0, free.Row, free.Index).position);
            yield return Until(() => game.Rack.Count == count + 1, 5, "a tile from the deck");
            Assert.That(game.Rack[free], Is.Not.EqualTo(RackGrid.Empty), "the ghost dropped on a slot: the drawn tile takes it");
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator DraggingARackTileToAnEmptySlotMovesIt()
        {
            yield return Build("open");
            int tile = LooseTile(); var free = game.Rack.FirstFree().Value;
            yield return Drag(game.Board.ViewOf(tile).transform.position, table.SlotPose(0, free.Row, free.Index).position);
            yield return Settle(.3f);
            game.Rack.TryFind(tile, out var at); Assert.That(at, Is.EqualTo(free));
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator HoldingASetMovesItTogether()
        {
            yield return Build("open");
            var set = game.Rack.Segments().First(g => g.Ids.Length >= 3 && g.Ids.All(id => RackEval.Of(game.Rack, game.Model).ValidTiles.Contains(id)));
            int other = 1 - set.Row; var target = new RackSlot(other, 1);
            Vector2 a = ScreenOf(game.Board.ViewOf(set.Ids[0]).transform.position), b = ScreenOf(table.SlotPose(0, target.Row, target.Index).position);
            yield return Pointer(a, false); yield return Pointer(a, true);
            float until = Time.realtimeSinceStartup + .5f; while (Time.realtimeSinceStartup < until) yield return Pointer(a, true);   // hold 360 ms: the set
            yield return Drag(a, b, held: true);
            yield return Settle(.3f);
            var at = set.Ids.Select(id => { game.Rack.TryFind(id, out var s2); return s2; }).ToList();
            Assert.That(at.All(x => x.Row == other), Is.True, "the whole set moved to the other row");
            for (int i = 1; i < at.Count; i++) Assert.That(at[i].Index, Is.EqualTo(at[0].Index + i), "in order, side by side");
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator TheTakenTileDraggedBackToItsRingGoesBack()
        {
            yield return Build("play");
            var pile = game.Model.Discards[game.Abs(3)]; int side = TileIds.ToInt(pile[pile.Count - 1]), piled = pile.Count;
            yield return Click(game.Board.ViewOf(side).transform.position);
            yield return Until(() => game.PickedTile == side, 5, "the left tile taken");
            yield return Settle(.8f);
            yield return Drag(game.Board.ViewOf(side).transform.position, table.DiscardCenter(3));
            yield return Until(() => game.PickedTile < 0 && game.Model.Discards[game.Abs(3)].Count == piled, 5, "the tile back on its pile");
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator DraggingOntoALayOffTargetLaysItOff()
        {
            yield return Build("isle");
            TableGame.LayTarget target = null; int tile = -1;
            foreach (var id in game.Rack.Tiles.ToList())
            {
                game.SetSelected(id); var t = game.Targets.FirstOrDefault(x => x.Okey < 0);
                if (t != null) { target = t; tile = id; break; }
            }
            game.SetSelected(-1);
            Assume.That(target, Is.Not.Null, "a rack tile that fits a table meld");
            yield return Drag(game.Board.ViewOf(tile).transform.position, target.Pose.position);
            yield return Until(() => !game.Rack.TryFind(tile, out _), 5, "the tile laid off");
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator DraggingTheRealTileOntoTheOkeyTakesIt()
        {
            yield return Build("isle");
            int y4 = game.Rack.Tiles.First(id => TileIds.Code(id) == "Y4");
            game.SetSelected(y4); var target = game.Targets.FirstOrDefault(x => x.Okey >= 0); game.SetSelected(-1);
            Assume.That(target, Is.Not.Null, "G16: Y4 has an okey target");
            yield return Drag(game.Board.ViewOf(y4).transform.position, target.Pose.position);
            yield return Until(() => game.Rack.TryFind(target.Okey, out _), 5, "the okey on my rack");
        }
    }
}
