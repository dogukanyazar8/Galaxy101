using System;
using System.Collections;
using System.IO;
using System.Linq;
using Galaxy101.Table;
using NUnit.Framework;
using Okey101.Core.Model;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    /// <summary>Live table: engine-driven deal, turns, bots, my draw/discard and the rack input's ray mapping.</summary>
    public sealed class TableGameRuntimeTests
    {
        private GameObject gameOwner, sceneOwner, hudOwner;
        private string statePath;
        private float savedTimeScale;
        private Mouse mouse;
        private InputSettings.EditorInputBehaviorInPlayMode? savedBehavior;
        private InputSettings.BackgroundBehavior? savedBackground;

        private TableGame Build(int human, uint seed, bool withHud)
        {
            savedTimeScale = Time.timeScale;
            // TableGame first: TableScene.Awake sees it and skips the static demo layout.
            gameOwner = new GameObject("TableGame"); var game = gameOwner.AddComponent<TableGame>(); game.Configure(human, seed);
            sceneOwner = new GameObject("GalaxyTable"); sceneOwner.AddComponent<TableScene>();
            if (withHud)
            {
                statePath = Path.Combine(Path.GetTempPath(), "galaxy-table-game-" + Guid.NewGuid().ToString("N") + ".json");
                hudOwner = new GameObject("TableHud"); hudOwner.SetActive(false);
                hudOwner.AddComponent<UIDocument>().panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
                var hud = hudOwner.AddComponent<TableHud>(); hudOwner.SetActive(true); hud.Initialize(statePath);
                gameOwner.AddComponent<RackInput>();
            }
            return game;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = savedTimeScale;
            if (mouse != null) { InputSystem.RemoveDevice(mouse); mouse = null; }
            if (savedBackground.HasValue) { InputSystem.settings.backgroundBehavior = savedBackground.Value; savedBackground = null; }
            if (savedBehavior.HasValue) { InputSystem.settings.editorInputBehaviorInPlayMode = savedBehavior.Value; savedBehavior = null; }
            foreach (var go in new[] { hudOwner, gameOwner, sceneOwner }) if (go != null) UnityEngine.Object.Destroy(go);
            yield return null;
            if (statePath != null && File.Exists(statePath)) File.Delete(statePath);
        }

        private static IEnumerator Until(Func<bool> condition, float realSeconds, string what)
        {
            float end = Time.realtimeSinceStartup + realSeconds;
            while (!condition()) { if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what); yield return null; }
        }

        private static void AssertBoardMatchesEngine(TableGame game)
        {
            var view = game.Session.GetView(TableGame.Me);
            Assert.That(game.Rack.Tiles.OrderBy(x => x), Is.EqualTo(view.OwnHand.Select(t => t.Value).OrderBy(x => x)), "my rack holds exactly my hand");
            foreach (var id in game.Rack.Tiles) Assert.That(game.Board.ViewOf(id), Is.Not.Null, "rack tile " + id + " has a view");
            for (int k = 1; k < 4; k++) Assert.That(game.Board.HiddenCount(k), Is.EqualTo(view.Players[k].HandCount), "seat " + k + " hidden tiles");
            foreach (var pile in view.Discards) foreach (var id in pile) Assert.That(game.Board.ViewOf(id.Value), Is.Not.Null, "discard " + id + " visible");
            foreach (var meld in view.Table) foreach (var t in meld.Group.Tiles) Assert.That(game.Board.ViewOf(t.Id.Value), Is.Not.Null, "meld tile visible");
        }

        [UnityTest]
        [Timeout(420000)]
        public IEnumerator BotsPlayAWholeHandAndTheBoardTracksEveryTile()
        {
            var game = Build(-1, 4242, true);
            Time.timeScale = 6;
            yield return Until(() => game.Current == TableGame.Phase.Opponent, 30, "the deal to finish");
            int checks = 0; long lastChecked = -1; float deadline = Time.realtimeSinceStartup + 380;
            while (game.LastResult == null)
            {
                var view = game.Session.GetView(TableGame.Me);
                // Check each engine version once the board has reconciled it (commands complete asynchronously).
                if (view.Result == null && game.SyncedVersion == view.Version && view.Version != lastChecked && game.Current == TableGame.Phase.Opponent)
                { lastChecked = view.Version; AssertBoardMatchesEngine(game); checks++; }
                if (Time.realtimeSinceStartup > deadline) Assert.Fail("The hand did not finish in time; turns=" + game.TurnsPlayed);
                yield return null;
            }
            Assert.That(game.TurnsPlayed, Is.GreaterThan(4));
            Assert.That(checks, Is.GreaterThan(4));

            // Finish banner, then the hand result card (T24) with every seat, then the next hand.
            var hud = hudOwner.GetComponent<TableHud>();
            yield return Until(() => game.ResultOpen, 30, "the hand result card");
            yield return null;
            Assert.That(hud.Canvas.Q<VisualElement>("table-result-card"), Is.Not.Null);
            for (int k = 0; k < 4; k++) Assert.That(hud.Canvas.Q<VisualElement>("result-row-" + k), Is.Not.Null, "result row " + k);
            Assert.That(hud.Canvas.Q<Label>("result-next-label").text, Does.StartWith("Sonraki el").Or.StartWith("Next hand"));
            string capture = Environment.GetEnvironmentVariable("GALAXY_TABLE_RESULT_CAPTURE");
            if (!string.IsNullOrEmpty(capture)) { Time.timeScale = 1; yield return Capture(hudOwner.GetComponent<UIDocument>(), capture); Time.timeScale = 6; }
            Debug.Log("GALAXY101_TABLE_SOAK turns=" + game.TurnsPlayed + " reason=" + game.LastResult.FinishInfo.Reason + " checks=" + checks);
            game.NextHand();
            yield return Until(() => game.HandNumber == 2 && !game.ResultOpen, 30, "the next hand");
            Assert.That(game.Current, Is.EqualTo(TableGame.Phase.Deal).Or.EqualTo(TableGame.Phase.Opponent).Or.EqualTo(TableGame.Phase.Draw).Or.EqualTo(TableGame.Phase.Discard));
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator IDiscardDrawAndDiscardAgainWhileBotsPlay()
        {
            var game = Build(TableGame.Me, 777, true);
            Time.timeScale = 1;
            yield return Until(() => game.Current == TableGame.Phase.Seating, 10, "the waiting table");
            Assert.That(game.Seated, Is.EqualTo(new[] { true, false, false, false }));
            var waitingHud = hudOwner.GetComponent<TableHud>();
            yield return null;
            Assert.That(waitingHud.Canvas.Q<VisualElement>("table-waiting"), Is.Not.Null);
            Assert.That(waitingHud.Canvas.Q<VisualElement>("invite-1"), Is.Not.Null);
            yield return Until(() => game.Seated[2], 5, "Ayşe to join at 1.3 s");
            string seatCapture = Environment.GetEnvironmentVariable("GALAXY_TABLE_SEAT_CAPTURE");
            if (!string.IsNullOrEmpty(seatCapture)) yield return Capture(hudOwner.GetComponent<UIDocument>(), seatCapture);
            game.Invite(1);
            Assert.That(game.Seated[1], Is.True, "Davet et fills the seat at once");
            yield return Until(() => game.Countdown > 0, 10, "the 3-2-1 countdown");
            Time.timeScale = 3;
            yield return Until(() => game.CanDiscard, 40, "my first discard turn (I start with 22 tiles)");
            Assert.That(game.Rack.Count, Is.EqualTo(22));
            yield return Until(() => game.SyncedVersion == game.Session.GetView(TableGame.Me).Version, 5, "the board to reconcile");
            AssertBoardMatchesEngine(game);

            // The rack input maps a screen point through the HUD canvas onto the very tile drawn there.
            yield return null;
            var input = gameOwner.GetComponent<RackInput>();
            var hud = hudOwner.GetComponent<TableHud>();
            int probe = game.Rack.Tiles.First();
            var cam = Camera.allCameras.First(c => c.targetTexture != null && c.CompareTag("MainCamera"));
            var vp = cam.WorldToViewportPoint(game.Board.ViewOf(probe).Target.position);
            var canvasPoint = new Vector2(vp.x * TableHud.Width, (1 - vp.y) * TableHud.Height);
            var panelPoint = hud.Canvas.LocalToWorld(canvasPoint);
            float scale = hud.Canvas.panel.visualTree.worldBound.width / Screen.width;
            var screen = new Vector2(panelPoint.x / scale, Screen.height - panelPoint.y / scale);
            Assert.That(input.TryRay(screen, out var ray), Is.True);
            Physics.SyncTransforms();
            Assert.That(Physics.RaycastAll(ray, 10).Select(h => h.collider.GetComponent<TileView>()).Any(v => v != null && v.Id == probe), Is.True, "ray hits the probed tile");

            int first = game.Rack.Tiles.Last();
            game.Discard(first);
            yield return Until(() => !game.Rack.Tiles.Contains(first) && game.Current == TableGame.Phase.Opponent, 10, "my discard to be accepted");
            Assert.That(game.Session.GetView(TableGame.Me).Discards[0].Last().Value, Is.EqualTo(first));

            yield return Until(() => game.CanDraw, 90, "my draw turn after three bots");
            Assert.That(game.TurnsPlayed, Is.GreaterThanOrEqualTo(4), "me and three bots played");
            yield return Until(() => game.SyncedVersion == game.Session.GetView(TableGame.Me).Version, 5, "the board to reconcile");
            AssertBoardMatchesEngine(game);
            string capture = Environment.GetEnvironmentVariable("GALAXY_TABLE_GAME_CAPTURE");
            if (!string.IsNullOrEmpty(capture)) yield return Capture(hudOwner.GetComponent<UIDocument>(), capture);

            var task = game.Draw(DrawSource.Stock, null);
            yield return Until(() => task.IsCompleted, 10, "the draw");
            Assert.That(task.Result, Is.True);
            Assert.That(game.Rack.Count, Is.EqualTo(22));
            Assert.That(game.LastDrawn, Is.GreaterThanOrEqualTo(0));
            game.Discard(game.LastDrawn);
            yield return Until(() => game.Current == TableGame.Phase.Opponent || game.HudState().Flash != null, 10, "the second discard");
            if (game.Current != TableGame.Phase.Opponent)
            {
                // Rule corner (e.g. a laid-off-able joker): any other tile is fine.
                game.Discard(game.Rack.Tiles.First(id => id != game.LastDrawn));
                yield return Until(() => game.Current == TableGame.Phase.Opponent, 10, "the fallback discard");
            }
            Assert.That(game.Rack.Count, Is.EqualTo(21));
        }

        /// <summary>World point → screen pixels (bottom-left), the inverse of RackInput.TryRay.</summary>
        private static Vector2 ScreenOf(TableHud hud, Vector3 world)
        {
            var cam = Camera.allCameras.First(c => c.targetTexture != null && c.CompareTag("MainCamera"));
            var vp = cam.WorldToViewportPoint(world);
            var panelPoint = hud.Canvas.LocalToWorld(new Vector2(vp.x * TableHud.Width, (1 - vp.y) * TableHud.Height));
            float scale = hud.Canvas.panel.visualTree.worldBound.width / Screen.width;
            return new Vector2(panelPoint.x / scale, Screen.height - panelPoint.y / scale);
        }

        private IEnumerator Pointer(Vector2 position, bool pressed)
        {
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position, buttons = (ushort)(pressed ? 1 : 0) });
            yield return null; yield return null;
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator PointerDragsOnTheRackAndDoubleClickDiscards()
        {
            var game = Build(TableGame.Me, 9001, true);
            // Batch editors have no focused Game view; let device input reach play mode regardless.
            savedBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            savedBackground = InputSystem.settings.backgroundBehavior; InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            mouse = InputSystem.AddDevice<Mouse>();
            yield return Until(() => game.CanDiscard && game.SyncedVersion == game.Session.GetView(TableGame.Me).Version, 40, "my first turn");
            yield return new WaitForSeconds(1.5f); // tiles settle after the deal
            var hud = hudOwner.GetComponent<TableHud>(); var table = sceneOwner.GetComponent<TableScene>();
            Assert.That(game.Rack.TryFind(game.Rack[0, 2], out var from), Is.True);
            int id = game.Rack[0, 2]; Assert.That(game.Rack[0, 14], Is.EqualTo(RackGrid.Empty));

            // Drag: press on the tile, move in steps (> 5 px), release over the empty top-right slot.
            Vector2 a = ScreenOf(hud, game.Board.ViewOf(id).Target.position), b = ScreenOf(hud, table.SlotPose(0, 0, 14).position);
            yield return Pointer(a, false); yield return Pointer(a, true);
            for (int i = 1; i <= 8; i++) yield return Pointer(Vector2.Lerp(a, b, i / 8f), true);
            yield return Pointer(b, false);
            Assert.That(game.Rack[0, 14], Is.EqualTo(id), "the dragged tile lands in the empty slot");
            Assert.That(game.Rack[0, 2], Is.EqualTo(RackGrid.Empty));
            yield return new WaitForSeconds(0.6f);

            // Double click a tile within 380 ms: discard.
            int victim = game.Rack[1, 5];
            Vector2 c = ScreenOf(hud, game.Board.ViewOf(victim).Target.position);
            yield return Pointer(c, false); yield return Pointer(c, true); yield return Pointer(c, false); yield return Pointer(c, true); yield return Pointer(c, false);
            yield return Until(() => !game.Rack.Tiles.Contains(victim), 10, "the double-click discard");
            Assert.That(game.Session.GetView(TableGame.Me).Discards[0].Last().Value, Is.EqualTo(victim));
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator MenuOpensTheGalaxyTableAndExitReturnsToTheMenu()
        {
            savedTimeScale = Time.timeScale;
            var load = SceneManager.LoadSceneAsync("Bootstrap", LoadSceneMode.Single);
            while (!load.isDone) yield return null;
            MainMenuController menu = null;
            yield return Until(() => (menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>()) != null && menu.View != null, 30, "the main menu");
            var open = menu.StartLocalAsync();
            yield return Until(() => open.IsCompleted, 60, "the table scene to load");
            Assert.That(open.IsFaulted, Is.False, open.Exception?.ToString());
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(MainMenuController.GalaxyTableScene));
            TableGame game = null;
            yield return Until(() => (game = UnityEngine.Object.FindAnyObjectByType<TableGame>()) != null && game.Current != TableGame.Phase.Waiting, 30, "the deal");

            game.RequestExit();
            yield return null;
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(MainMenuController.GalaxyTableScene), "the exit button opens the confirmation card");
            Assert.That(game.ExitOpen, Is.True);
            game.CancelExit(); Assert.That(game.ExitOpen, Is.False, "Masada kal closes the card");
            game.RequestExit(); game.ConfirmExit();
            yield return Until(() => SceneManager.GetActiveScene().name == "Bootstrap" && UnityEngine.Object.FindAnyObjectByType<MainMenuController>() != null, 60, "the menu after leaving");
            Assert.That(UnityEngine.Object.FindAnyObjectByType<TableGame>(), Is.Null);
        }

        private static IEnumerator Capture(UIDocument document, string path)
        {
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var panel = UnityEngine.Object.Instantiate(document.panelSettings); var original = document.panelSettings;
            panel.targetTexture = target; document.panelSettings = panel;
            for (int i = 0; i < 20; i++) yield return null;
            yield return new WaitForSecondsRealtime(1.2f); // entrance animations settle
            var previous = RenderTexture.active; RenderTexture.active = target;
            var pixels = new Texture2D(1280, 720, TextureFormat.RGBA32, false); pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            RenderTexture.active = previous; File.WriteAllBytes(path, pixels.EncodeToPNG());
            document.panelSettings = original; UnityEngine.Object.Destroy(panel); target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(pixels);
            Debug.Log("GALAXY101_TABLE_GAME_CAPTURE " + path);
        }
    }
}
