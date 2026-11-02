using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    public sealed class LogoPerspectiveRuntimeTests
    {
        private GameObject owner;
        private MainMenuController menu;
        private PanelSettings panel;
        private string path;
        private bool? oldAuth;
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldAuth = MainMenuController.AuthOnStart; MainMenuController.AuthOnStart = false;
            path = Path.Combine(Path.GetTempPath(), "g101-logo-" + Guid.NewGuid().ToString("N") + ".json");
            owner = new GameObject("Native perspective logo test"); owner.SetActive(false);
            panel = UnityEngine.Object.Instantiate(Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings"));
            owner.AddComponent<UIDocument>().panelSettings = panel;
            menu = owner.AddComponent<MainMenuController>(); owner.SetActive(true); menu.Initialize(path);
            double deadline = Time.realtimeSinceStartupAsDouble + 15;
            while (AnimationTime() < .6f && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(AnimationTime(), Is.GreaterThanOrEqualTo(.6f));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (owner != null) UnityEngine.Object.Destroy(owner);
            if (panel != null) UnityEngine.Object.Destroy(panel);
            MainMenuController.AuthOnStart = oldAuth;
            yield return null; yield return null;
            if (File.Exists(path)) File.Delete(path);
        }

        private float AnimationTime() => (float)typeof(MenuView).GetField("animationTime", Private).GetValue(menu.View);
        private static GameObject World() => Resources.FindObjectsOfTypeAll<GameObject>().Single(o => o != null && o.name == "Galaxy101 logo perspective world");

        [UnityTest]
        public IEnumerator NativeWorldUiMatchesTheCssProjectionAndRemainsVisibleWhileFocusPausesRendering()
        {
            float time = AnimationTime(); menu.View.SetFocused(false);
            var world = World(); var document = world.GetComponent<UIDocument>(); var camera = world.GetComponentInChildren<Camera>();
            Assert.That(document.panelSettings.renderMode, Is.EqualTo(PanelRenderMode.WorldSpace));
            Assert.That(document.panelSettings, Is.Not.SameAs(Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings")));
            Assert.That(world.GetComponents<Renderer>().Length, Is.GreaterThan(0), "A native world-space UI Renderer must exist.");
            Assert.That(world.GetComponents<Renderer>().All(r => r.forceRenderingOff), Is.True, "The logo must be excluded from unrelated cameras.");
            var tile = document.rootVisualElement.Q<VisualElement>("logo-tile-0");
            foreach (var point in new[] { Vector2.zero, new Vector2(78, 0), new Vector2(78, 108), new Vector2(0, 108) })
            {
                Vector3 viewport = camera.WorldToViewportPoint(tile.worldTransform.MultiplyPoint3x4(point));
                var actual = new Vector2(424 + viewport.x * 432, 112 + (1 - viewport.y) * 320);
                var expected = LogoTileMotion.Project(0, time, point);
                Assert.That(Vector2.Distance(actual, expected), Is.LessThan(.02f), "Native perspective corner differs from CSS ordering/origin."
                    + " time=" + time.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) + " point=" + point.ToString("F6")
                    + " actual=" + actual.ToString("F6") + " expected=" + ((Vector2)expected).ToString("F6")
                    + " layout=" + tile.layout.ToString("F6") + " styleTop=" + tile.style.top.value.value.ToString("F6", System.Globalization.CultureInfo.InvariantCulture));
            }
            yield return null; yield return null;
            Assert.That(camera.enabled, Is.False, "Focus loss pauses the extra render pass.");
            Assert.That(menu.View.Root.Q<Image>("logo-perspective").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "A paused logo keeps its last visible frame.");
            var previous = RenderTexture.active; var output = camera.targetTexture;
            Assert.That(output, Is.Not.Null); RenderTexture.active = output;
            var pixels = new Texture2D(output.width, output.height, TextureFormat.RGBA32, false);
            try
            {
                pixels.ReadPixels(new Rect(0, 0, output.width, output.height), 0, 0); pixels.Apply();
                Assert.That(pixels.GetPixels32().Count(p => p.a > 32 && p.r > 120 && p.g > 100), Is.GreaterThan(100), "Native text/rounded tile never reached the perspective framebuffer.");
                // 20px past the tile's bottom is outside its crisp face and 5px ledge. Only the source soft shadows reach it.
                var shadowPoint = LogoTileMotion.Project(0, time, new Vector2(39, 128));
                int shadowX = Mathf.RoundToInt((shadowPoint.x - 424) / 432 * (output.width - 1));
                int shadowY = Mathf.RoundToInt((1 - (shadowPoint.y - 112) / 320) * (output.height - 1));
                Assert.That(shadowX, Is.InRange(0, output.width - 1)); Assert.That(shadowY, Is.InRange(0, output.height - 1));
                Assert.That(pixels.GetPixel(shadowX, shadowY).a, Is.GreaterThan(.025f), "The source Gaussian box-shadows must remain visible beyond the crisp tile/ledge.");
                string directory = Environment.GetEnvironmentVariable("GALAXY_HTML_CAPTURE_DIR");
                if (!string.IsNullOrEmpty(directory)) { Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, "logo-perspective-entry.png"), pixels.EncodeToPNG()); }
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.Destroy(pixels); }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CompositedPerspectiveDotKeepsTheSourceGammaColor()
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 15;
            while (AnimationTime() < 1.12f && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            float time = AnimationTime(); Assert.That(time, Is.LessThan(LogoTileMotion.EntryEnd));
            menu.View.SetFocused(false);
            yield return null; yield return null;
            var presenter = owner.GetComponent<MenuGammaPresenter>(); Assert.That(presenter, Is.Not.Null);
            var output = presenter.Output; Assert.That(output, Is.Not.Null);
            Vector2 point = LogoTileMotion.Project(1, time, new Vector2(44, 93));
            var canvas = menu.View.Root.Q<VisualElement>("galaxy-canvas"); point = canvas.LocalToWorld(point);
            Rect bounds = menu.View.Root.panel.visualTree.worldBound;
            int x = Mathf.RoundToInt((point.x - bounds.x) / bounds.width * (output.width - 1));
            int y = Mathf.RoundToInt((1 - (point.y - bounds.y) / bounds.height) * (output.height - 1));
            Assert.That(x, Is.InRange(0, output.width - 1)); Assert.That(y, Is.InRange(0, output.height - 1));
            var prior = RenderTexture.active; var sample = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = output; sample.ReadPixels(new Rect(x, y, 1, 1), 0, 0); sample.Apply();
                Color32 actual = sample.GetPixels32()[0], expected = menu.View.C("var(--t2)");
                string context = "Composited dot at " + x + "," + y + " actual=" + actual + " expected=" + expected;
                Assert.That(Mathf.Abs(actual.r - expected.r), Is.LessThanOrEqualTo(2), context);
                Assert.That(Mathf.Abs(actual.g - expected.g), Is.LessThanOrEqualTo(2), context);
                Assert.That(Mathf.Abs(actual.b - expected.b), Is.LessThanOrEqualTo(2), context);
            }
            finally { RenderTexture.active = prior; UnityEngine.Object.Destroy(sample); }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SettlingTheEntryReleasesTheExtraCameraPanelAndFramebuffer()
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 15;
            while (AnimationTime() < 1.7f && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            yield return null; yield return null;
            Assert.That(menu.View.Root.Q<Image>("logo-perspective"), Is.Null);
            Assert.That(Resources.FindObjectsOfTypeAll<GameObject>().Any(o => o != null && o.name == "Galaxy101 logo perspective world"), Is.False);
            Assert.That(Resources.FindObjectsOfTypeAll<RenderTexture>().Any(o => o != null && o.name == "Galaxy101 logo perspective framebuffer"), Is.False);
            Assert.That(Resources.FindObjectsOfTypeAll<PanelSettings>().Any(o => o != null && o.name == "Galaxy101 logo perspective panel"), Is.False);
            for (int i = 0; i < 3; i++) Assert.That(menu.View.Root.Q<VisualElement>("logo-tile-" + i).resolvedStyle.opacity, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }
    }
}
