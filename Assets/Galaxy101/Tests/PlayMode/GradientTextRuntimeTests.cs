using System;
using System.Collections;
using System.Linq;
using Galaxy101.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    public sealed class GradientTextRuntimeTests
    {
        private GameObject owner;
        private UIDocument document;
        private PanelSettings panel;
        private RenderTexture target;
        private Texture2D pixels;
        private IDisposable binding;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("A graphics device is required for native glyph gradient rendering.");
            owner = new GameObject("Native three-stop text gradient"); owner.SetActive(false);
            panel = UnityEngine.Object.Instantiate(Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings"));
            target = new RenderTexture(512, 160, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            target.Create(); panel.targetTexture = target; panel.scaleMode = PanelScaleMode.ConstantPixelSize; panel.scale = 1;
            panel.forceGammaRendering = true; panel.clearColor = true; panel.colorClearValue = Color.clear;
            document = owner.AddComponent<UIDocument>(); document.panelSettings = panel; owner.SetActive(true);
            document.rootVisualElement.style.width = 512; document.rootVisualElement.style.height = 160;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            binding?.Dispose(); binding = null;
            if (owner != null) UnityEngine.Object.Destroy(owner);
            if (panel != null) UnityEngine.Object.Destroy(panel);
            if (pixels != null) UnityEngine.Object.Destroy(pixels);
            if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThreeStopsReachTheNativeGlyphPixelsWithoutChangingTheLabelOrItsDump()
        {
            var reference = Label("HHH", "reference", 16);
            var label = Label("HHH", "gradient-caption", 248);
            for (int i = 0; i < 8; i++) yield return null;
            var oldSize = label.layout.size;
            var oldTextSize = label.MeasureTextSize(label.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined);
            int dumpCount = G101Dump.Texts().Count(row => (string)row[0] == "HHH");
            binding = CssGradientText.Attach(label, Color.red, Color.green, Color.blue);
            for (int i = 0; i < 8; i++) yield return null;
            ReadPixels();

            Assert.That(document.rootVisualElement.Q<Label>("gradient-caption"), Is.SameAs(label));
            Assert.That(label.childCount, Is.Zero, "The measured Label must stay a leaf.");
            Assert.That(label.layout.size, Is.EqualTo(oldSize));
            Assert.That(label.MeasureTextSize(label.text, 0, VisualElement.MeasureMode.Undefined, 0, VisualElement.MeasureMode.Undefined), Is.EqualTo(oldTextSize));
            Assert.That(G101Dump.Texts().Count(row => (string)row[0] == "HHH"), Is.EqualTo(dumpCount), "The rendering pass must not duplicate diagnostic text.");

            int[] samples = new int[3];
            for (int y = 0; y < 88; y++)
            {
                float position = (y + .5f) / 88;
                int band = position >= .2f && position <= .35f ? 0 : position >= .46f && position <= .54f ? 1 : position >= .65f && position <= .8f ? 2 : -1;
                if (band < 0) continue;
                var expected = position < .5f
                    ? new Color32((byte)Mathf.RoundToInt(255 * (1 - position * 2)), (byte)Mathf.RoundToInt(255 * position * 2), 0, 255)
                    : new Color32(0, (byte)Mathf.RoundToInt(255 * (2 - position * 2)), (byte)Mathf.RoundToInt(255 * (position * 2 - 1)), 255);
                for (int x = 0; x < 200; x++)
                {
                    var mask = Pixel(reference, x, y);
                    if (mask.a < 250 || mask.r < 250 || mask.g < 250 || mask.b < 250) continue;
                    var actual = Pixel(label, x, y);
                    AssertColor(actual, expected, "glyph band " + band + ", y=" + y);
                    samples[band]++;
                }
            }
            foreach (int count in samples) Assert.That(count, Is.GreaterThan(15), "Every gradient segment must reach opaque glyph pixels.");
        }

        [UnityTest]
        public IEnumerator SplitGlyphsPreserveTheNativeShadowAndDisposeOnlyTheirOwnRenderingPass()
        {
            var reference = Label("HHH", "reference", 16);
            var label = Label("HHH", "gradient-caption", 248);
            var shadow = new TextShadow { offset = new Vector2(0, 3), blurRadius = 0, color = Color.magenta };
            reference.style.textShadow = label.style.textShadow = shadow;
            for (int i = 0; i < 8; i++) yield return null;
            int count = document.rootVisualElement.childCount;
            var previous = label.PostProcessTextVertices;
            binding = CssGradientText.Attach(label, Color.red, Color.green, Color.blue, .35f);
            for (int i = 0; i < 8; i++) yield return null;
            ReadPixels();
            int shadowSamples = 0;
            for (int y = 0; y < 100; y++)
                for (int x = 0; x < 200; x++)
                {
                    var mask = Pixel(reference, x, y);
                    if (mask.a < 245 || mask.r < 245 || mask.b < 245 || mask.g > 10) continue;
                    AssertColor(Pixel(label, x, y), mask, "native hard shadow at " + x + "," + y);
                    shadowSamples++;
                }
            Assert.That(shadowSamples, Is.GreaterThan(10));
            Assert.That(document.rootVisualElement.childCount, Is.EqualTo(count + 1));
            binding.Dispose(); binding = null;
            yield return null;
            Assert.That(document.rootVisualElement.childCount, Is.EqualTo(count));
            Assert.That(label.PostProcessTextVertices, Is.EqualTo(previous));
            Assert.That(label.parent, Is.SameAs(document.rootVisualElement));
            binding = CssGradientText.Attach(label, Color.red, Color.green, Color.blue);
            yield return null; yield return null;
            label.RemoveFromHierarchy();
            yield return null;
            Assert.That(document.rootVisualElement.childCount, Is.EqualTo(count - 1), "Detaching the owner also removes its absolute rendering pass.");
            Assert.That(label.PostProcessTextVertices, Is.EqualTo(previous));
        }

        private Label Label(string text, string name, float x)
        {
            var label = new Label(text) { name = name, enableRichText = false, pickingMode = PickingMode.Ignore };
            label.style.position = Position.Absolute; label.style.left = x; label.style.top = 24; label.style.width = 200; label.style.height = 88;
            label.style.marginLeft = label.style.marginRight = label.style.marginTop = label.style.marginBottom = 0;
            label.style.paddingLeft = label.style.paddingRight = label.style.paddingTop = label.style.paddingBottom = 0;
            label.style.unityFont = Resources.Load<Font>("Galaxy101/Fonts/Montserrat-800"); label.style.fontSize = 72;
            label.style.whiteSpace = WhiteSpace.NoWrap; label.style.unityTextAlign = TextAnchor.MiddleLeft; label.style.color = Color.white;
            document.rootVisualElement.Add(label); return label;
        }

        private void ReadPixels()
        {
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, true);
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
            }
            finally { RenderTexture.active = previous; }
        }

        private Color32 Pixel(Label label, int x, int y)
        {
            var point = label.LocalToWorld(new Vector2(x + .5f, y + .5f));
            return pixels.GetPixel(Mathf.FloorToInt(point.x), target.height - 1 - Mathf.FloorToInt(point.y));
        }

        private static void AssertColor(Color32 actual, Color32 expected, string location)
        {
            Assert.That((int)actual.r, Is.EqualTo((int)expected.r).Within(10), location + " red");
            Assert.That((int)actual.g, Is.EqualTo((int)expected.g).Within(10), location + " green");
            Assert.That((int)actual.b, Is.EqualTo((int)expected.b).Within(10), location + " blue");
            Assert.That((int)actual.a, Is.EqualTo((int)expected.a).Within(10), location + " alpha");
        }
    }
}
