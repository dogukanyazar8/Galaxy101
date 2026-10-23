using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    /// <summary>
    /// Ayarlar → Görüntü (spec/19, S07) and layout at other screen shapes: the 1280×720 canvas stays whole and centred
    /// at 16:9, 16:10, 21:9, 4:3 and 4K (panel "Expand" scaling), nothing spills outside it.
    /// </summary>
    public sealed class DisplaySettingsTests
    {
        private GameObject owner;
        private string statePath;
        private readonly List<UnityEngine.Object> temporary = new List<UnityEngine.Object>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (owner != null) UnityEngine.Object.Destroy(owner);
            foreach (var o in temporary) if (o != null) UnityEngine.Object.Destroy(o);
            temporary.Clear();
            yield return null;
            FramePacing.Apply(FramePacing.Auto);
            if (statePath != null && File.Exists(statePath)) File.Delete(statePath);
        }

        private MainMenuController Menu()
        {
            statePath = Path.Combine(Path.GetTempPath(), "galaxy-display-" + Guid.NewGuid().ToString("N") + ".json");
            owner = new GameObject("Menu"); owner.SetActive(false);
            owner.AddComponent<UIDocument>().panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            var menu = owner.AddComponent<MainMenuController>(); owner.SetActive(true); menu.Initialize(statePath);
            return menu;
        }

        private static void Click(VisualElement root, string name)
        {
            var e = root.Q<VisualElement>(name); Assert.That(e, Is.Not.Null, "element " + name);
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = e; e.SendEvent(evt); }
        }

        [UnityTest]
        public IEnumerator DisplayTabHasModeResolutionAndFrameRateAndAppliesThem()
        {
            var menu = Menu(); yield return null;
            Assert.That(GameDisplay.CurrentMode, Is.EqualTo(GameDisplay.Mode.Fullscreen), "Tam ekran by default");
            Assert.That(GameDisplay.Resolution, Is.EqualTo(GameDisplay.Native));

            menu.View.OpenPanel("settings", 2); yield return null;
            var root = menu.View.Root;
            var rows = root.Query<VisualElement>(className: "g-settings-row").ToList();
            var titles = rows.Select(r => r.Query<Label>().First().text).ToList();
            Assert.That(titles, Is.EqualTo(new[] { "Arka plan hareketi", "Görüntü modu", "Çözünürlük", "Kare hızı", "Efekt kalitesi" }).Or.EqualTo(new[] { "Background motion", "Display mode", "Resolution", "Frame rate", "Effect quality" }),
                "spec/19 order; no planet row");
            Assert.That(root.Q<VisualElement>("setting-wmode-fullscreen").ClassListContains("selected"), Is.True);

            Click(root, "setting-wmode-borderless"); yield return null;
            Assert.That((string)menu.Model.Settings["wmode"], Is.EqualTo("borderless"));
            Assert.That(GameDisplay.LastRequest.Mode, Is.EqualTo(FullScreenMode.FullScreenWindow));
            Click(root, "setting-wmode-windowed"); yield return null;
            Assert.That(GameDisplay.LastRequest.Mode, Is.EqualTo(FullScreenMode.Windowed));

            var native = GameDisplay.NativeSize; var options = GameDisplay.Options(native);
            Assert.That(root.Q<Label>("setting-res-value").text, Is.EqualTo(GameDisplay.Label(native)), "native size by default");
            Assert.That(root.Q<VisualElement>("setting-res-next").ClassListContains("at-end"), Is.True);
            if (options.Count > 1)
            {
                Click(root, "setting-res-prev"); yield return null;
                var smaller = options[options.Count - 2];
                Assert.That((string)menu.Model.Settings["res"], Is.EqualTo(GameDisplay.Format(smaller)));
                Assert.That(root.Q<Label>("setting-res-value").text, Is.EqualTo(GameDisplay.Label(smaller)));
                Assert.That(GameDisplay.LastRequest.Width, Is.LessThanOrEqualTo(smaller.x));
            }

            // ←/→ on the focused stepper.
            var fps = root.Q<VisualElement>("setting-fps");
            using (var key = KeyDownEvent.GetPooled('\0', KeyCode.RightArrow, EventModifiers.None)) { key.target = fps; fps.SendEvent(key); }
            yield return null;
            Assert.That((int)menu.Model.Settings["fps"], Is.EqualTo(30));

            // Varsayılana dön: Tam ekran, native, Otomatik.
            Click(root, "settings-reset"); yield return null;
            Assert.That((string)menu.Model.Settings["wmode"], Is.EqualTo("fullscreen")); Assert.That((string)menu.Model.Settings["res"], Is.EqualTo(GameDisplay.Native));
            Assert.That((int)menu.Model.Settings["fps"], Is.EqualTo(FramePacing.Auto));
            Assert.That(GameDisplay.LastRequest.Mode, Is.EqualTo(FullScreenMode.ExclusiveFullScreen));

            string dir = Environment.GetEnvironmentVariable("GALAXY_DISPLAY_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(dir)) { Click(root, "setting-wmode-windowed"); yield return null; yield return Shot(owner.GetComponent<UIDocument>(), 1280, 720, Path.Combine(dir, "display_settings.png")); }
        }

        private static readonly Vector2Int[] Shapes = { new Vector2Int(1280, 720), new Vector2Int(1920, 1200), new Vector2Int(2560, 1080), new Vector2Int(1024, 768), new Vector2Int(3840, 2160), new Vector2Int(1366, 768) };

        [UnityTest]
        public IEnumerator MenuAndPanelsStayWholeAndCentredAtEveryScreenShape()
        {
            var menu = Menu(); yield return null;
            var document = owner.GetComponent<UIDocument>();
            string dir = Environment.GetEnvironmentVariable("GALAXY_DISPLAY_CAPTURE_DIR");
            foreach (var shape in Shapes)
            {
                foreach (var panel in new string[] { null, "settings", "shop" })
                {
                    if (panel == null) menu.View.CloseTop(); else menu.View.OpenPanel(panel);
                    yield return Shot(document, shape.x, shape.y, string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, "shape_" + shape.x + "x" + shape.y + "_" + (panel ?? "menu") + ".png"),
                        root => AssertCanvasWhole(root, shape, panel ?? "menu"));
                }
            }
        }

        /// <summary>The canvas is 1280×720 design units, fully inside the screen, centred, scaled by min(w/1280, h/720).</summary>
        internal static void AssertCanvasWhole(VisualElement root, Vector2Int shape, string where)
        {
            var canvas = root.Q<VisualElement>("galaxy-canvas") ?? root.Q<VisualElement>("table-canvas");
            Assert.That(canvas, Is.Not.Null);
            var screen = root.worldBound; var c = canvas.worldBound;
            Assert.That(c.width, Is.EqualTo(1280).Within(1), where + " canvas keeps its design width");
            Assert.That(c.xMin, Is.GreaterThanOrEqualTo(screen.xMin - 1)); Assert.That(c.xMax, Is.LessThanOrEqualTo(screen.xMax + 1), where + " " + shape + " not cropped sideways");
            Assert.That(c.yMin, Is.GreaterThanOrEqualTo(screen.yMin - 1)); Assert.That(c.yMax, Is.LessThanOrEqualTo(screen.yMax + 1), where + " " + shape + " not cropped vertically");
            Assert.That(c.center.x, Is.EqualTo(screen.center.x).Within(1)); Assert.That(c.center.y, Is.EqualTo(screen.center.y).Within(1), where + " centred");
            // One of the sides touches the screen edge (Expand: the canvas is as big as the screen allows).
            Assert.That(Mathf.Min(screen.width - c.width, screen.height - c.height), Is.LessThan(2), where + " " + shape + " fills one axis");
            var problems = new List<string>();
            canvas.Query<Label>().ForEach(l =>
            {
                if (string.IsNullOrEmpty(l.text) || l.resolvedStyle.display == DisplayStyle.None || l.resolvedStyle.visibility == Visibility.Hidden || l.resolvedStyle.opacity <= .01f) return;
                for (var e = l.parent; e != null && e != canvas; e = e.parent) if (e is ScrollView || e.resolvedStyle.display == DisplayStyle.None) return;
                var r = l.worldBound;
                if (r.xMin < c.xMin - 1 || r.xMax > c.xMax + 1 || r.yMin < c.yMin - 1 || r.yMax > c.yMax + 1) problems.Add("'" + l.text + "' " + r);
            });
            Assert.That(problems, Is.Empty, where + " " + shape + " text outside the canvas");
        }

        /// <summary>Render the panel into a W×H target (the panel then lays out at that screen shape), check, save.</summary>
        internal static IEnumerator Shot(UIDocument document, int width, int height, string path, Action<VisualElement> check = null)
        {
            var target = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var panel = UnityEngine.Object.Instantiate(document.panelSettings); var original = document.panelSettings;
            panel.targetTexture = target; document.panelSettings = panel;
            for (int i = 0; i < 12; i++) yield return null;
            yield return new WaitForSecondsRealtime(.6f);
            try
            {
                check?.Invoke(document.rootVisualElement);
                if (path != null)
                {
                    var previous = RenderTexture.active; RenderTexture.active = target;
                    var pixels = new Texture2D(width, height, TextureFormat.RGBA32, false); pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0); pixels.Apply();
                    RenderTexture.active = previous; File.WriteAllBytes(path, pixels.EncodeToPNG()); UnityEngine.Object.Destroy(pixels);
                }
            }
            finally { document.panelSettings = original; UnityEngine.Object.Destroy(panel); target.Release(); UnityEngine.Object.Destroy(target); }
            yield return null;
        }
    }
}
