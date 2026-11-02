using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    public sealed class SplashLayoutTests
    {
        private GameObject owner;
        private PanelSettings panel;
        private RenderTexture target;
        private VisualElement splash, canvas;
        private AuthFlow auth;
        private string path;
        private bool? previousAuth;
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            previousAuth = MainMenuController.AuthOnStart; MainMenuController.AuthOnStart = false;
            GameClock.Reset(42);
            path = Path.Combine(Path.GetTempPath(), "g101-splash-layout-" + Guid.NewGuid().ToString("N") + ".json");
            owner = new GameObject("Splash source layout fixture"); owner.SetActive(false);
            panel = UnityEngine.Object.Instantiate(Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings"));
            // Fresh reference was measured at DPR1.5. Own that framebuffer so Editor window DPI cannot alter font rounding.
            target = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear); target.Create();
            panel.targetTexture = target; panel.scaleMode = PanelScaleMode.ConstantPixelSize; panel.scale = 1.5f;
            panel.forceGammaRendering = true; panel.clearColor = true; panel.colorClearValue = Color.clear;
            owner.AddComponent<UIDocument>().panelSettings = panel;
            var menu = owner.AddComponent<MainMenuController>(); owner.SetActive(true); menu.Initialize(path); menu.View.SetFocused(false);
            // Menu initialization normally replaces this with its screen-sized gamma panel. This layout fixture owns the target.
            owner.GetComponent<MenuGammaPresenter>().enabled = false;
            owner.GetComponent<UIDocument>().panelSettings = panel;
            canvas = menu.View.Root.Q<VisualElement>("galaxy-canvas") ?? menu.View.Root;
            splash = new VisualElement(); MenuView.Place(splash, 0, 0, 1280, 720); canvas.Add(splash);
            // Exercise the production splash renderer directly, without a real account/session refresh.
            auth = new AuthFlow(menu.View, owner.transform, null);
            Set("body", splash); Set("layer", splash); Set("screen", "splash");
            float began = Time.realtimeSinceStartup - 1.8f;
            Set("splashStarted", began); Set("splashStepAt", began); Set("splashTarget", 84f); Set("splashRandom", GameClock.Stream("splash"));
            typeof(AuthFlow).GetMethod("BuildSplash", Private).Invoke(auth, null);
            GameClock.Freeze(0);
            for (int i = 0; i < 6; i++) yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            auth?.Hide();
            if (owner != null) UnityEngine.Object.Destroy(owner);
            if (panel != null) UnityEngine.Object.Destroy(panel);
            MainMenuController.AuthOnStart = previousAuth; GameClock.Reset(42);
            yield return null;
            if (target != null) { if (RenderTexture.active == target) RenderTexture.active = null; target.Release(); UnityEngine.Object.Destroy(target); }
            if (path != null && File.Exists(path)) File.Delete(path);
        }

        private void Set(string field, object value) => typeof(AuthFlow).GetField(field, Private).SetValue(auth, value);
        private float Top(VisualElement element) => canvas.WorldToLocal(element.worldBound.position).y;

        [UnityTest]
        public IEnumerator SplashLogoUsesItsCssLineBoxAndKeepsTheTilesAtTheSourceRow()
        {
            var logo = splash.Q<Image>("auth-logo"); Assert.That(logo, Is.Not.Null);
            Assert.That(logo.parent.layout.height, Is.EqualTo(75.9f).Within(1), "HTML font:700 66px/1.15 consumes a 75.9 px line; the padded 154 px artwork cannot participate in column flow.");
            Assert.That(Top(logo.parent), Is.EqualTo(118).Within(1));
            Assert.That(logo.style.top.value.value, Is.EqualTo(-37.089f).Within(.001f), "The artwork cap at row105/2 aligns with the source font cap inside that line.");
            var tile = splash.Q<VisualElement>("auth-tile-0");
            Assert.That(Top(tile.parent), Is.EqualTo(203.89584f).Within(1), "DOM wrapper geometry is compared before the inner tile's rotation/bob, in canvas units rather than framebuffer pixels.");
            Assert.That(tile.resolvedStyle.paddingTop, Is.EqualTo(8));
            Assert.That(tile.Q<Label>().layout.height, Is.EqualTo(64).Within(1), "Splash numerals have a 64 px CSS line rather than the font's 81 px natural metrics.");
            yield break;
        }

        [UnityTest]
        public IEnumerator SplashProgressKeepsTheFullFourteenPixelTrackBelowItsLabels()
        {
            var progress = splash.Q<VisualElement>("splash-progress");
            var track = progress.ElementAt(1);
            Assert.That(track.layout.height, Is.EqualTo(14).Within(.01f), "The source progress container is auto-height; a 40 px parent must not shrink the 14 px track.");
            Assert.That(Top(track), Is.EqualTo(509.6667f).Within(1));
            Assert.That(progress.layout.height, Is.EqualTo(45.6667f).Within(1));
            yield break;
        }

        [UnityTest]
        public IEnumerator FrozenSplashUsesTheCanonicalCssFloatPhase()
        {
            Assert.That(GameClock.NowMs, Is.EqualTo(100000));
            var left = splash.Q<VisualElement>("auth-tile-0");
            var middle = splash.Q<VisualElement>("auth-tile-1");
            var right = splash.Q<VisualElement>("auth-tile-2");
            Assert.That(middle.style.translate.value.y.value, Is.EqualTo(0).Within(.001f), "g101Float at canonical100s with -2s delay is at its zero-lift endpoint.");
            Assert.That(left.style.translate.value.y.value, Is.EqualTo(-5.449602f).Within(.001f));
            Assert.That(right.style.translate.value.y.value, Is.EqualTo(-5.449602f).Within(.001f));
            yield break;
        }
    }
}
