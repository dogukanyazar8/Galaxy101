using System;
using System.Collections;
using System.IO;
using System.Linq;
using Galaxy101.Table;
using NUnit.Framework;
using Okey101.Core.V4;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    public sealed class TableTwoDimensionalTests
    {
        private GameObject sceneOwner, hudOwner, gameOwner;
        private string statePath;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (hudOwner != null) UnityEngine.Object.Destroy(hudOwner);
            if (sceneOwner != null) UnityEngine.Object.Destroy(sceneOwner);
            if (gameOwner != null) UnityEngine.Object.Destroy(gameOwner);
            TableGame.Pending = null;
            yield return null;
            if (statePath != null && File.Exists(statePath)) File.Delete(statePath);
        }

        [UnityTest]
        public IEnumerator FlatViewSuspendsOnlyTheCameraAndRestoresThePreviousPerspective()
        {
            statePath = Path.Combine(Path.GetTempPath(), "g101-flat-" + Guid.NewGuid().ToString("N") + ".json");
            sceneOwner = new GameObject("Flat table scene"); sceneOwner.AddComponent<TableScene>();
            hudOwner = new GameObject("Flat table HUD"); hudOwner.SetActive(false);
            hudOwner.AddComponent<UIDocument>().panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            var hud = hudOwner.AddComponent<TableHud>(); hudOwner.SetActive(true); hud.Initialize(statePath);
            hud.Settings.Set("view", "mid");
            hud.ToggleTwoDimensional();
            yield return null;
            Assert.That(hud.IsTwoDimensional, Is.True);
            Assert.That(hud.Table.CameraRig.Camera.enabled, Is.False);
            Assert.That(hud.Table.gameObject.activeInHierarchy, Is.True, "The game scene must keep processing match events.");
            Assert.That(hud.Canvas.Q<VisualElement>("table-flat-rack"), Is.Not.Null);
            Assert.That(hud.Canvas.Q<VisualElement>("table.viewToggle"), Is.Not.Null);
            hud.ToggleTwoDimensional();
            yield return null;
            Assert.That(hud.Settings.View, Is.EqualTo("mid"));
            Assert.That(hud.Table.CameraRig.Camera.enabled, Is.True);
        }

        [Test]
        public void FlatViewIsAValidatedPersistedTableChoice()
        {
            var settings = new TableSettings(null, false);
            settings.Set("view", "2d");
            Assert.That(settings.View, Is.EqualTo("2d"));
            Assert.Throws<ArgumentException>(() => settings.Set("view", "2D"));
            Assert.That(settings.View, Is.EqualTo("2d"));
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator FlatRackDragUsesTheLiveRackAndSurvivesReturningToThreeDimensions()
        {
            TableGame.Pending = new TableLaunch
            {
                Setup = new TableSetup { Options = new MatchOptions { Mode = "solo", Hands = 5 }, Seed = 42, TurnMs = 60000 },
                Table = TableGame.DefaultTable(), Flow = "play", Clock = () => Time.timeAsDouble * 1000
            };
            gameOwner = new GameObject("Flat live game"); var game = gameOwner.AddComponent<TableGame>();
            sceneOwner = new GameObject("Flat live scene"); sceneOwner.AddComponent<TableScene>();
            statePath = Path.Combine(Path.GetTempPath(), "g101-flat-live-" + Guid.NewGuid().ToString("N") + ".json");
            hudOwner = new GameObject("Flat live HUD"); hudOwner.SetActive(false);
            hudOwner.AddComponent<UIDocument>().panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            var hud = hudOwner.AddComponent<TableHud>(); hudOwner.SetActive(true); hud.Initialize(statePath);
            float until = Time.realtimeSinceStartup + 12;
            while (!game.CanArrange && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(game.CanArrange, Is.True, "The actual match must have a dealt rack before testing input.");
            var session = game.Session; var hand = game.Rack.Tiles.OrderBy(x => x).ToArray();
            hud.Settings.Set("view", "2d"); hud.SyncFlatTable(true); yield return null; yield return null;
            int id = hand[0]; var free = game.Rack.FirstFree(); Assert.That(free.HasValue, Is.True);
            var tile = hud.Canvas.Q<VisualElement>("flat-tile-" + id); Assert.That(tile, Is.Not.Null);
            var tiles = hud.Canvas.Q<VisualElement>("table-flat-tiles");
            var meldTile = tiles.Q<VisualElement>("flat-tile-" + TileIds.ToInt(game.Model.Melds[0].Tiles[0]));
            var indicatorTile = tiles.Q<VisualElement>("flat-tile-" + TileIds.ToInt(game.Indicator));
            Assert.That(tiles.IndexOf(tile), Is.GreaterThan(tiles.IndexOf(meldTile)), "Source rack z=6 stays above meld z=5, independent of creation order.");
            Assert.That(tiles.IndexOf(meldTile), Is.GreaterThan(tiles.IndexOf(indicatorTile)), "Source meld z=5 stays above indicator z=4.");
            var pile = game.Model.Discards[game.Abs(1)];
            Assert.That(pile.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(tiles.IndexOf(tiles.Q<VisualElement>("flat-tile-" + TileIds.ToInt(pile[1]))), Is.GreaterThan(tiles.IndexOf(tiles.Q<VisualElement>("flat-tile-" + TileIds.ToInt(pile[0])))), "Discard depth uses source z=3+index.");
            var picked = tile.panel.Pick(tile.worldBound.center);
            Assert.That(picked == tile || picked != null && tile.Contains(picked), Is.True, "The pointer fixture must hit this settled tile, outside overlapping entry animations.");
            var layer = hud.Canvas.Q<VisualElement>("table-flat"); var box = Table2DLayout.RackTile(free.Value);
            Vector2 destination = layer.LocalToWorld(new Vector2(box.X + 25, box.Y + 35));
            using (var down = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = tile.worldBound.center })) tile.SendEvent(down);
            using (var move = PointerMoveEvent.GetPooled(new Event { type = EventType.MouseDrag, button = 0, mousePosition = destination })) layer.SendEvent(move);
            using (var up = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = destination })) layer.SendEvent(up);
            yield return null;
            Assert.That(game.Rack.TryFind(id, out var moved), Is.True);
            Assert.That(moved, Is.EqualTo(free.Value));
            hud.SyncFlatTable(true);
            Assert.That(tile.style.left.value.value, Is.EqualTo(box.X), "An instantaneous sync must cancel any active tile slide.");
            Assert.That(tile.style.top.value.value, Is.EqualTo(box.Y));
            CollectionAssert.AreEquivalent(hand, game.Rack.Tiles, "Rearranging the flat view must conserve every physical tile.");
            Assert.That(game.Session, Is.SameAs(session));
            var output = Environment.GetEnvironmentVariable("GALAXY_HTML_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(output))
            {
                Directory.CreateDirectory(output);
                yield return DisplaySettingsTests.Shot(hudOwner.GetComponent<UIDocument>(), 1280, 720, Path.Combine(output, "table-2d-gameplay.png"));
            }
            hud.ToggleTwoDimensional(); yield return null;
            Assert.That(game.Session, Is.SameAs(session));
            Assert.That(game.Rack.TryFind(id, out var returned), Is.True); Assert.That(returned, Is.EqualTo(free.Value));
            Assert.That(game.Board.ViewOf(id).Dragged, Is.False);
        }

        [Test]
        public void SavedFlatViewSurvivesSettingsReload()
        {
            bool had = PlayerPrefs.HasKey(TableSettings.PrefsKey); string previous = PlayerPrefs.GetString(TableSettings.PrefsKey);
            bool defaults = TableSettings.DesignDefaultsOnly;
            try
            {
                TableSettings.DesignDefaultsOnly = false;
                PlayerPrefs.SetString(TableSettings.PrefsKey, "{\"view\":\"2d\"}");
                Assert.That(new TableSettings(null).View, Is.EqualTo("2d"));
            }
            finally
            {
                TableSettings.DesignDefaultsOnly = defaults;
                if (had) PlayerPrefs.SetString(TableSettings.PrefsKey, previous); else PlayerPrefs.DeleteKey(TableSettings.PrefsKey);
            }
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator ValidSetsGetNoGreenRingAndSelectionKeepsItsRing()
        {
            TableGame.Pending = new TableLaunch
            {
                Setup = new TableSetup { Options = new MatchOptions { Mode = "solo", Hands = 5 }, Seed = 42, TurnMs = 60000 },
                Table = TableGame.DefaultTable(), Flow = "play", Clock = () => Time.timeAsDouble * 1000
            };
            gameOwner = new GameObject("Flat valid-set game"); var game = gameOwner.AddComponent<TableGame>();
            sceneOwner = new GameObject("Flat valid-set scene"); sceneOwner.AddComponent<TableScene>();
            statePath = Path.Combine(Path.GetTempPath(), "g101-flat-valid-set-" + Guid.NewGuid().ToString("N") + ".json");
            hudOwner = new GameObject("Flat valid-set HUD"); hudOwner.SetActive(false);
            hudOwner.AddComponent<UIDocument>().panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            var hud = hudOwner.AddComponent<TableHud>(); hudOwner.SetActive(true); hud.Initialize(statePath);
            float until = Time.realtimeSinceStartup + 12;
            while (!game.CanArrange && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(game.CanArrange, Is.True, "The match must be ready before arranging the deterministic presentation fixture.");

            // Keep the real hand's physical ids: R9a is already in the public left pile and R12a is in a public meld.
            var hand = game.Rack.Tiles.ToArray();
            int[] run = new[] { "R10", "R11", "R12" }.Select(code => hand.Single(id => TileCodes.Code(id) == code)).ToArray();
            int isolated = hand.Single(id => TileCodes.Code(id) == "Y7");
            game.Rack.Clear();
            for (int i = 0; i < run.Length; i++) game.Rack.Set(new RackSlot(0, i), run[i]);
            game.Rack.Set(new RackSlot(0, 5), isolated);
            var remaining = hand.Where(id => !run.Contains(id) && id != isolated).ToArray();
            for (int i = 0; i < remaining.Length; i++) game.Rack.Set(i < 7 ? new RackSlot(0, 8 + i) : new RackSlot(1, i - 7), remaining[i]);
            CollectionAssert.AreEquivalent(hand, game.Rack.Tiles, "The fixture must conserve the actual hand.");
            Assert.That(game.Model.Discards.SelectMany(pile => pile).Any(id => run.Contains(TileIds.ToInt(id))), Is.False);
            hud.Settings.Set("view", "2d"); hud.Render(game.HudState()); hud.SyncFlatTable(true);
            yield return null; yield return null;
            // "Perleri vurgula" is gone: a valid set looks like any other rack tile (the + tag still marks işlek tiles).
            VisualElement Ring(int id) => hud.Canvas.Q<VisualElement>("flat-tile-" + id).Q<VisualElement>("flat-tile-ring");
            foreach (int id in run) Assert.That(Ring(id).resolvedStyle.borderTopWidth, Is.EqualTo(0), "A valid set has no highlight ring.");
            Assert.That(Ring(isolated).resolvedStyle.borderTopWidth, Is.EqualTo(0));

            var tile = hud.Canvas.Q<VisualElement>("flat-tile-" + run[0]);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = tile; tile.SendEvent(submit); }
            yield return null;
            Assert.That(Ring(run[0]).style.borderTopWidth.value, Is.EqualTo(3));
            Assert.That(Ring(run[0]).resolvedStyle.borderTopWidth, Is.GreaterThan(0));
            Assert.That(Ring(run[0]).resolvedStyle.borderTopColor, Is.EqualTo(hud.View.C("var(--a1)")), "The selected tile keeps the gold ring.");
            Assert.That(Ring(run[1]).resolvedStyle.borderTopWidth, Is.EqualTo(0));
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator RealOkeyUsesTheFlatOkeyFaceWhileFakeAndIndicatorKeepTheirPrintedFaces()
        {
            TableGame.Pending = new TableLaunch
            {
                Setup = new TableSetup { Options = new MatchOptions { Mode = "solo", Hands = 5 }, Seed = 42, TurnMs = 60000 },
                Table = TableGame.DefaultTable(), Flow = "play", Clock = () => Time.timeAsDouble * 1000
            };
            gameOwner = new GameObject("Flat okey game"); var game = gameOwner.AddComponent<TableGame>();
            sceneOwner = new GameObject("Flat okey scene"); sceneOwner.AddComponent<TableScene>();
            statePath = Path.Combine(Path.GetTempPath(), "g101-flat-okey-" + Guid.NewGuid().ToString("N") + ".json");
            hudOwner = new GameObject("Flat okey HUD"); hudOwner.SetActive(false);
            hudOwner.AddComponent<UIDocument>().panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            var hud = hudOwner.AddComponent<TableHud>(); hudOwner.SetActive(true); hud.Initialize(statePath);
            float until = Time.realtimeSinceStartup + 12;
            while ((!game.HumanTurn || !game.Model.OkeyKnown) && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(game.HumanTurn && game.Model.OkeyKnown, Is.True);

            int real = TileIds.ToInt(TileIds.Code(game.Model.OkeyColor, game.Model.OkeyNumber) + "a");
            int fake = TileIds.ToInt("Ja");
            int normal = TileIds.ToInt(game.Model.IsOkey("R1a") ? "B1a" : "R1a");
            game.Rack.Clear(); game.Rack.Set(new RackSlot(0, 0), real); game.Rack.Set(new RackSlot(0, 2), fake); game.Rack.Set(new RackSlot(0, 4), normal);
            hud.Settings.Set("view", "2d"); hud.Render(game.HudState());
            yield return null; yield return null;
            VisualElement Tile(int id) => hud.Canvas.Q<VisualElement>("flat-tile-" + id);
            Assert.That(Tile(real).Q<Image>().resolvedStyle.display, Is.EqualTo(DisplayStyle.None), "A real okey must not expose its ordinary printed number.");
            Assert.That(Tile(real).Query<Label>().ToList().Any(label => label.text == "OKEY" && label.resolvedStyle.display != DisplayStyle.None && label.worldBound.height > 0), Is.True);
            Assert.That(Tile(real).tooltip, Is.EqualTo("OKEY"));
            Assert.That(Tile(fake).Q<Image>().resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "The fake okey keeps the authored joker atlas cell.");
            Assert.That(Tile(normal).Q<Image>().resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            int indicator = TileIds.ToInt(game.Indicator);
            Assert.That(Tile(indicator).Q<Image>().resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "The indicator keeps its printed face.");
        }
    }
}
