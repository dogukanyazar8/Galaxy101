using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    public sealed class SettingsParityTests
    {
        private GameObject owner;
        private PanelSettings panel;
        private MainMenuController menu;
        private string path, cursor;
        private bool? previousAuth;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousAuth = MainMenuController.AuthOnStart; MainMenuController.AuthOnStart = false;
            cursor = Audio.GalaxyCursor.Mode;
            path = Path.Combine(Path.GetTempPath(), "galaxy-settings-parity-" + Guid.NewGuid().ToString("N") + ".json");
            owner = new GameObject("HTML settings parity"); owner.SetActive(false);
            panel = UnityEngine.Object.Instantiate(Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings"));
            owner.AddComponent<UIDocument>().panelSettings = panel;
            menu = owner.AddComponent<MainMenuController>(); owner.SetActive(true); menu.Initialize(path);
            yield return null; yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (owner != null) UnityEngine.Object.Destroy(owner);
            if (panel != null) UnityEngine.Object.Destroy(panel);
            MainMenuController.AuthOnStart = previousAuth; Audio.GalaxyCursor.Mode = cursor;
            FramePacing.Apply(FramePacing.Auto);
            yield return null;
            if (File.Exists(path)) File.Delete(path);
        }

        private static void Click(VisualElement root, string name)
        {
            var target = root.Q<VisualElement>(name); Assert.That(target, Is.Not.Null, name);
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = target; target.SendEvent(evt); }
        }

        [UnityTest]
        public IEnumerator DisplayDraftDoesNotChangeMotionFrameRateOrCursorUntilApplied()
        {
            var root = menu.View.Root;
            bool originalMotion = (bool)menu.Model.Settings["motion"];
            int originalFps = (int)menu.Model.Settings["fps"];
            menu.View.OpenPanel("settings", 2); yield return null;
            Click(root, "setting-motion");
            Click(root, "setting-fps-next"); yield return null;
            var cursorRow = root.Q<VisualElement>("setting-cursor");
            Click(cursorRow, "menu.o.pick[1]"); yield return null;
            Assert.That((bool)menu.Model.Settings["motion"], Is.EqualTo(originalMotion));
            Assert.That((int)menu.Model.Settings["fps"], Is.EqualTo(originalFps), "selecting an FPS draft must not change pacing");
            Assert.That(Audio.GalaxyCursor.Mode, Is.EqualTo(cursor), "cursor selection is staged with display settings");
            Click(root, "menu.closeSettings#4"); yield return null; yield return null;
            Assert.That((bool)menu.Model.Settings["motion"], Is.EqualTo(!originalMotion));
            Assert.That((int)menu.Model.Settings["fps"], Is.Not.EqualTo(originalFps));
            Assert.That(Audio.GalaxyCursor.Mode, Is.EqualTo(Audio.CursorSet.Large));
            Assert.That(menu.View.Modals.Panel, Is.Null, "Save applies the display draft and closes the window");
        }

        [UnityTest]
        public IEnumerator CancellingDisplayDraftKeepsAppliedPreferences()
        {
            var root = menu.View.Root;
            var original = menu.Model.Settings.DeepClone();
            menu.View.OpenPanel("settings", 2); yield return null;
            Click(root, "setting-motion"); Click(root, "setting-fps-next"); yield return null;
            Click(root, "menu.closeSettings#3"); yield return null; yield return null;
            Assert.That(Newtonsoft.Json.Linq.JToken.DeepEquals(menu.Model.Settings, original), Is.True);
            menu.View.OpenPanel("settings", 2); yield return null;
            Assert.That(root.Q<VisualElement>("setting-motion").ClassListContains("on"), Is.EqualTo((bool)original["motion"]));
        }

        [UnityTest]
        public IEnumerator GameplayAndSoundApplyImmediatelyAndSurviveCancel()
        {
            var root = menu.View.Root;
            bool original = (bool)menu.Model.Settings["dbl"];
            menu.View.OpenPanel("settings", 0); yield return null;
            Click(root, "setting-dbl"); yield return null;
            Assert.That((bool)menu.Model.Settings["dbl"], Is.EqualTo(!original), "HTML upd writes game choices immediately");
            menu.View.SetTab("settings", 1); yield return null;
            bool originalSound = (bool)menu.Model.Settings["tileSnd"];
            Click(root, "setting-tileSnd"); yield return null;
            Assert.That((bool)menu.Model.Settings["tileSnd"], Is.EqualTo(!originalSound));
            Click(root, "menu.closeSettings#3"); yield return null;
            var restored = MenuModel.LoadResources(path);
            Assert.That((bool)restored.Settings["dbl"], Is.EqualTo(!original));
            Assert.That((bool)restored.Settings["tileSnd"], Is.EqualTo(!originalSound));
        }

        [UnityTest]
        public IEnumerator CounterAndAnimatedCursorRemainAvailableAcrossMainAndSettings()
        {
            var root = menu.View.Root;
            Assert.That(root.Q<VisualElement>("game-cursor"), Is.Not.Null);
            var play = root.Q<VisualElement>("menu.play");
            using (var move = PointerMoveEvent.GetPooled(new Event { type = EventType.MouseMove, mousePosition = play.worldBound.center })) play.SendEvent(move);
            yield return null;
            Assert.That(root.Q<VisualElement>("game-cursor-pointer").ClassListContains("auth-cursor-active"), Is.True);
            var pointer = root.Q<Image>("game-cursor-pointer"); var saturn = pointer.image;
            Assert.That(menu.View.Services.Settings.SetPlanet("Mars"), Is.True); yield return new WaitForSecondsRealtime(.1f);
            Assert.That(pointer.image, Is.Not.SameAs(saturn), "The cursor uses the selected planet's original gradient.");
            Assert.That(menu.View.Services.Settings.SetPlanet("Satürn"), Is.True); yield return new WaitForSecondsRealtime(.1f);
            Assert.That(pointer.image, Is.SameAs(saturn));
            menu.View.OpenPanel("settings", 2); yield return null;
            Click(root, "setting-showFps"); yield return null;
            Assert.That((bool)menu.Model.Settings["showFps"], Is.False);
            Click(root, "menu.closeSettings#4"); yield return new WaitForSecondsRealtime(.7f);
            Assert.That((bool)menu.Model.Settings["showFps"], Is.True);
            Assert.That(root.Q<Label>("app.fps").text, Does.Match(@"FPS \d+ · \d+\.\d ms"));
            var output = Environment.GetEnvironmentVariable("GALAXY_HTML_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(output))
            {
                Directory.CreateDirectory(output);
                menu.View.OpenPanel("settings", 2); yield return new WaitForSecondsRealtime(.4f);
                yield return DisplaySettingsTests.Shot(owner.GetComponent<UIDocument>(), 1280, 720, Path.Combine(output, "display-settings.png"));
                Click(root, "menu.closeSettings#3"); yield return new WaitForSecondsRealtime(.4f);
                yield return DisplaySettingsTests.Shot(owner.GetComponent<UIDocument>(), 1280, 720, Path.Combine(output, "main-menu.png"));
            }
        }

        internal static IEnumerator SaveDisplayAndReopen(MainMenuController menu)
        {
            Click(menu.View.Root, "menu.closeSettings#4"); yield return new WaitForSecondsRealtime(.05f);
            menu.View.OpenPanel("settings", 2); yield return new WaitForSecondsRealtime(.05f);
        }

        [UnityTest]
        public IEnumerator DiagnosticDumpPreservesWholeLiteralTextAndMasksPasswordInputs()
        {
            string caption = "HTML karşılaştırması seksen karakterden uzun metni bütünüyle korumalı; <etiket> düz metindir, biçimlendirme değildir.";
            var canvas = menu.View.Root.Q<VisualElement>("galaxy-canvas");
            var label = menu.View.Text(caption, 13); label.name = "diagnostic.long-text";
            MenuView.Place(label, 12, 100, 600, 50); canvas.Add(label);
            const string secret = "private-test-value";
            var password = new TextField { name = "diagnostic.password", isPasswordField = true, value = secret };
            MenuView.Place(password, 12, 160, 300, 40); canvas.Add(password);
            yield return null; yield return null;
            var layout = Diagnostics.G101Dump.Layout("test", 42, 0);
            var field = ((Newtonsoft.Json.Linq.JArray)layout["items"]).First(item => (string)item["id"] == password.name);
            Assert.That((string)field["value"], Is.EqualTo(new string('•', secret.Length)));
            Assert.That(layout.ToString(), Does.Not.Contain(secret));
            var texts = Diagnostics.G101Dump.Texts();
            Assert.That(texts.Any(item => (string)item[0] == caption), Is.True);
            Assert.That(texts.ToString(), Does.Not.Contain(secret));
        }

        [UnityTest]
        public IEnumerator SoundDragAppliesContinuouslyAndPersistsAfterRelease()
        {
            Assert.That(menu.Model.Persist(), Is.True);
            menu.View.OpenPanel("settings", 1); yield return new WaitForSecondsRealtime(.1f);
            var root = menu.View.Root; var slider = root.Q<GalaxySlider>("setting-master");
            int previous = (int)menu.Model.Settings["master"];
            var track = slider.Q<VisualElement>(className: "slider-track").worldBound;
            var first = new Vector2(track.xMin + track.width * .25f, track.center.y);
            var second = new Vector2(track.xMin + track.width * .65f, track.center.y);
            using (var down = PointerDownEvent.GetPooled(new Event { type = EventType.MouseDown, button = 0, mousePosition = first })) slider.SendEvent(down);
            using (var move = PointerMoveEvent.GetPooled(new Event { type = EventType.MouseDrag, button = 0, mousePosition = second })) slider.SendEvent(move);
            yield return null;
            Assert.That(slider.IsDragging, Is.True);
            Assert.That(root.Q<GalaxySlider>("setting-master"), Is.SameAs(slider));
            Assert.That((int)menu.Model.Settings["master"], Is.EqualTo(65).Within(1));
            Assert.That((int)MenuModel.LoadResources(path).Settings["master"], Is.EqualTo(previous), "Do not rewrite the save file on every drag sample.");
            using (var up = PointerUpEvent.GetPooled(new Event { type = EventType.MouseUp, button = 0, mousePosition = second })) slider.SendEvent(up);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That((int)MenuModel.LoadResources(path).Settings["master"], Is.EqualTo((int)menu.Model.Settings["master"]));
        }
    }
}
