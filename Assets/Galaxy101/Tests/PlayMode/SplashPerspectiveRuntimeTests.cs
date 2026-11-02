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
    public sealed class SplashPerspectiveRuntimeTests
    {
        private GameObject owner;
        private PanelSettings panel;
        private AuthFlow auth;
        private VisualElement splash;
        private string path;
        private bool? previousAuth;
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousAuth = MainMenuController.AuthOnStart; MainMenuController.AuthOnStart = false; GameClock.Reset(42);
            path = Path.Combine(Path.GetTempPath(), "g101-splash-perspective-" + Guid.NewGuid().ToString("N") + ".json");
            owner = new GameObject("Native splash perspective fixture"); owner.SetActive(false);
            panel = UnityEngine.Object.Instantiate(Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings"));
            owner.AddComponent<UIDocument>().panelSettings = panel;
            var menu = owner.AddComponent<MainMenuController>(); owner.SetActive(true); menu.Initialize(path); menu.View.SetFocused(false);
            splash = new VisualElement(); MenuView.Place(splash, 0, 0, 1280, 720);
            (menu.View.Root.Q<VisualElement>("galaxy-canvas") ?? menu.View.Root).Add(splash);
            auth = new AuthFlow(menu.View, owner.transform, null);
            Set("body", splash); Set("layer", splash); Set("screen", "splash");
            float began = Time.realtimeSinceStartup - .5f;
            Set("splashStarted", began); Set("splashStepAt", began); Set("splashTarget", 84f); Set("splashRandom", GameClock.Stream("splash"));
            typeof(AuthFlow).GetMethod("BuildSplash", Private).Invoke(auth, null);
            float deadline = Time.realtimeSinceStartup + .3f;
            while (World() == null && Time.realtimeSinceStartup < deadline) yield return null;
            var ticker = (IVisualElementScheduledItem)typeof(AuthFlow).GetField("ticker", Private).GetValue(auth); ticker.Pause();
            yield return null; yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            auth?.Hide();
            if (owner != null) UnityEngine.Object.Destroy(owner);
            if (panel != null) UnityEngine.Object.Destroy(panel);
            MainMenuController.AuthOnStart = previousAuth; GameClock.Reset(42);
            yield return null; yield return null;
            if (path != null && File.Exists(path)) File.Delete(path);
        }

        private void Set(string field, object value) => typeof(AuthFlow).GetField(field, Private).SetValue(auth, value);
        private static GameObject World() => Resources.FindObjectsOfTypeAll<GameObject>().FirstOrDefault(o => o != null && o.name == "Galaxy101 splash perspective world");

        [UnityTest]
        public IEnumerator NativeSplashCornersAndPixelsUseTheSourceBottomOriginPerspective()
        {
            var world = World(); Assert.That(world, Is.Not.Null, "Splash tileIn needs the existing world-space UI renderer, configured for -80deg and bottom origins.");
            var document = world.GetComponent<UIDocument>(); var camera = world.GetComponentInChildren<Camera>();
            var presentation = typeof(AuthFlow).GetField("splashPresentation", Private).GetValue(auth);
            float time = (float)presentation.GetType().GetField("time", Private).GetValue(presentation);
            var tile = document.rootVisualElement.Q<VisualElement>("splash-tile-face-0");
            var motion = typeof(MenuView).Assembly.GetType("Galaxy101.SplashTileMotion"); Assert.That(motion, Is.Not.Null);
            foreach (var point in new[] { Vector2.zero, new Vector2(78, 0), new Vector2(78, 108), new Vector2(0, 108) })
            {
                var viewport = camera.WorldToViewportPoint(tile.worldTransform.MultiplyPoint3x4(point));
                var actual = new Vector2(424 + viewport.x * 432, 160 + (1 - viewport.y) * 336);
                var expected = (Vector3)motion.GetMethod("Project").Invoke(null, new object[] { 0, time, point });
                Assert.That(Vector2.Distance(actual, expected), Is.LessThan(.03f), "Native splash corner differs from the nested source transforms.");
            }
            Assert.That(world.GetComponents<Renderer>().All(r => r.forceRenderingOff), Is.True);
            var output = camera.targetTexture; Assert.That(output, Is.Not.Null);
            var prior = RenderTexture.active; var pixels = new Texture2D(output.width, output.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = output; pixels.ReadPixels(new Rect(0, 0, output.width, output.height), 0, 0); pixels.Apply();
                Assert.That(pixels.GetPixels32().Count(p => p.a > 32 && p.r > 120 && p.g > 100), Is.GreaterThan(100), "The reused native rounded tile/text must reach the splash framebuffer.");
            }
            finally { RenderTexture.active = prior; UnityEngine.Object.Destroy(pixels); }
            yield break;
        }

        [UnityTest]
        public IEnumerator ClosingTheSplashReleasesItsExtraCameraPanelAndFramebuffer()
        {
            Assert.That(World(), Is.Not.Null);
            auth.Hide(); yield return null; yield return null;
            Assert.That(World(), Is.Null);
            Assert.That(Resources.FindObjectsOfTypeAll<RenderTexture>().Any(t => t != null && t.name == "Galaxy101 splash perspective framebuffer"), Is.False);
            Assert.That(Resources.FindObjectsOfTypeAll<PanelSettings>().Any(p => p != null && p.name == "Galaxy101 splash perspective panel"), Is.False);
        }
    }
}
