using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    /// <summary>
    /// CSS shadow lengths are UI points (filters follow the panel scale, as a browser at 200 % zoom), and a button's
    /// ledge survives a hover filter (a filtered element renders offscreen, clipped to its filter's region).
    /// </summary>
    public sealed class FilterScaleTests
    {
        private GameObject owner;
        private PanelSettings panel;
        private RenderTexture target;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (owner != null) Object.Destroy(owner);
            if (panel != null) Object.Destroy(panel);
            if (target != null) { target.Release(); Object.Destroy(target); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator LedgeSurvivesAHoverFilter()
        {
            target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            panel = Object.Instantiate(Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings")); panel.targetTexture = target;
            owner = new GameObject("Ledge filter"); var document = owner.AddComponent<UIDocument>(); document.panelSettings = panel;
            yield return null;
            var root = document.rootVisualElement; root.style.backgroundColor = Color.black;
            var box = new VisualElement(); box.style.position = Position.Absolute; box.style.left = 100; box.style.top = 100; box.style.width = 60; box.style.height = 40;
            box.style.backgroundColor = Color.white; foreach (var r in new[] { 14f }) box.style.borderBottomLeftRadius = box.style.borderBottomRightRadius = box.style.borderTopLeftRadius = box.style.borderTopRightRadius = r;
            root.Add(box);
            GalaxyFilters.HardShadow(box, 6, Color.red);
            GalaxyFilters.SaturationAndBrightness(box, 1, 1.25f); // what a hover adds
            for (int i = 0; i < 6; i++) yield return null;
            var previous = RenderTexture.active; RenderTexture.active = target;
            var pixels = new Texture2D(1280, 720, TextureFormat.RGBA32, false); pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            RenderTexture.active = previous;
            // Under the box centre (x 130): the ledge spans y 140–146.
            int red = 0; for (int y = 139; y < 150; y++) { var c = pixels.GetPixel(130, 719 - y); if (c.r > .5f && c.g < .3f) red++; }
            Object.Destroy(pixels);
            Assert.That(red, Is.InRange(5, 8), "the 6 px ledge still shows under a filtered (hovered) button");
        }

        [UnityTest]
        public IEnumerator ShadowOffsetFollowsThePanelScale()
        {
            target = new RenderTexture(2560, 1440, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            panel = Object.Instantiate(Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings")); panel.targetTexture = target;
            owner = new GameObject("Filter scale"); var document = owner.AddComponent<UIDocument>(); document.panelSettings = panel;
            yield return null;
            var root = document.rootVisualElement; root.style.backgroundColor = Color.black;
            var box = new VisualElement(); box.style.position = Position.Absolute; box.style.left = 100; box.style.top = 100; box.style.width = 100; box.style.height = 40;
            box.style.backgroundColor = Color.white; root.Add(box);
            GalaxyFilters.Shadow(box, 0, 20, 0.01f, Color.red);
            for (int i = 0; i < 6; i++) yield return null;
            Assert.That(box.scaledPixelsPerPoint, Is.EqualTo(2).Within(.01f), "a 2560 × 1440 target draws the 1280 × 720 layout at 2×");

            var previous = RenderTexture.active; RenderTexture.active = target;
            var pixels = new Texture2D(2560, 1440, TextureFormat.RGBA32, false); pixels.ReadPixels(new Rect(0, 0, 2560, 1440), 0, 0); pixels.Apply();
            RenderTexture.active = previous;
            // Column through the box centre (x 150 pt = 300 px); the box ends at y 140 pt = 280 px.
            int red = 0;
            for (int y = 280; y < 400; y++) { var c = pixels.GetPixel(300, 1440 - 1 - y); if (c.r > .5f && c.g < .3f) red++; }
            Object.Destroy(pixels);
            Assert.That(red, Is.InRange(36, 44), "a 20 pt offset shows 40 px of shadow under the box at 2×");
        }

        [UnityTest]
        public IEnumerator GlowRadiusFollowsThePanelScale()
        {
            float one = 0, two = 0;
            foreach (int scale in new[] { 1, 2 })
            {
                target = new RenderTexture(1280 * scale, 720 * scale, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
                panel = Object.Instantiate(Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings")); panel.targetTexture = target;
                owner = new GameObject("Glow scale"); var document = owner.AddComponent<UIDocument>(); document.panelSettings = panel;
                yield return null;
                var root = document.rootVisualElement; root.style.backgroundColor = Color.black;
                var box = new VisualElement(); box.style.position = Position.Absolute; box.style.left = 100; box.style.top = 100; box.style.width = 100; box.style.height = 40;
                box.style.backgroundColor = Color.white; root.Add(box);
                GalaxyFilters.Shadow(box, 0, 0, 10, Color.red);
                for (int i = 0; i < 6; i++) yield return null;
                var previous = RenderTexture.active; RenderTexture.active = target;
                var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false); pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                RenderTexture.active = previous;
                // Sum of the glow's red below the box along its centre column, in points.
                float sum = 0; int x = 150 * scale, start = 140 * scale;
                for (int y = start; y < start + 80 * scale; y++) sum += pixels.GetPixel(x, target.height - 1 - y).r;
                if (scale == 1) one = sum; else two = sum / 2;
                Object.Destroy(pixels); Object.Destroy(owner); Object.Destroy(panel); target.Release(); Object.Destroy(target); owner = null; panel = null; target = null;
                yield return null;
            }
            Debug.Log("GLOW_EXTENT 1x=" + one + " 2x=" + two);
            Assert.That(two, Is.EqualTo(one).Within(one * .15f), "the glow covers the same points at 2× as at 1×");
        }
    }
}
