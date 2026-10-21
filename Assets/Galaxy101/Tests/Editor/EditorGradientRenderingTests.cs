using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    public sealed class EditorGradientRenderingTests
    {
        private object view;
        private object localization;
        private object surface;

        [SetUp]
        public void SetUp()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("A graphics device is required for radial gradient byte fixtures.");
            var model = Activator.CreateInstance(Require("MenuModel"), Resources.Load<TextAsset>("Galaxy101/content").text, Resources.Load<TextAsset>("Galaxy101/themes").text, null);
            localization = Activator.CreateInstance(Require("LocalizationBridge"), model);
            var services = Activator.CreateInstance(Require("MenuServices"), model);
            view = Activator.CreateInstance(Require("MenuView"), new VisualElement(), model, services, localization);
            surface = Activator.CreateInstance(Require("GalaxySurface"), view);
        }

        [TearDown]
        public void TearDown()
        {
            (view as IDisposable)?.Dispose();
            (localization as IDisposable)?.Dispose();
        }

        [Test]
        public void DefaultEllipseAtProfileCenterReachesEqualAxisStopsBeforeItsFarthestCorner()
        {
            WithRenderedGradient("radial-gradient(ellipse at 50% 20%,#000000,#ffffff)", pixels =>
            {
                // CSS farthest-corner ellipse: rx=.5*sqrt(2), ry=.8*sqrt(2).
                // Right and bottom axis endpoints are 1/sqrt(2), hence byte 180.
                AssertGray(pixels, 64, 25, 2, "profile center");
                AssertGray(pixels, 127, 25, 180, "right axis endpoint");
                AssertGray(pixels, 64, 127, 180, "bottom axis endpoint");
                AssertGray(pixels, 127, 127, 255, "farthest corner");
            });
        }

        [Test]
        public void OffsetCircleUsesItsActualFarthestCornerInsteadOfFixedPointSevenRadius()
        {
            WithRenderedGradient("radial-gradient(circle at 35% 30%,#000000,#ffffff)", pixels =>
            {
                // CSS radius=sqrt(.65^2+.70^2)=.9552487; axis ratios .6805/.7328.
                AssertGray(pixels, 44, 38, 1, "coin center");
                AssertGray(pixels, 127, 38, 174, "right axis endpoint");
                AssertGray(pixels, 44, 127, 187, "bottom axis endpoint");
                AssertGray(pixels, 127, 127, 255, "farthest corner");
            });
            WithRenderedGradient("radial-gradient(circle at 35% 30%,#000000,#ffffff)", pixels =>
            {
                // A 200x100 positioning box has radius sqrt(130^2+70^2)=147.6482px.
                AssertGray(pixels, 44, 38, 1, "rectangular circle center");
                AssertGray(pixels, 127, 38, 225, "rectangular right axis endpoint");
                AssertGray(pixels, 44, 127, 121, "rectangular bottom axis endpoint");
                AssertGray(pixels, 127, 127, 255, "rectangular farthest corner");
            }, 200, 100);
        }

        [Test]
        public void ShareEllipseHonorsExplicitSeventyByOneHundredTwentyPercentRadii()
        {
            WithRenderedGradient("radial-gradient(ellipse 70% 120% at 85% 50%,#000000,#ffffff)", pixels =>
            {
                // Explicit radii rx=.70, ry=1.20: right=.15/.70; top=.50/1.20.
                AssertGray(pixels, 108, 64, 1, "share center");
                AssertGray(pixels, 127, 64, 55, "right axis endpoint");
                AssertGray(pixels, 108, 0, 106, "top axis endpoint");
                AssertGray(pixels, 0, 0, 255, "outside terminal stop");
            });
        }

        [Test]
        public void AngledLinearGradientUsesPhysicalBoxAspectInsteadOfSquareTextureAspect()
        {
            WithRenderedGradient("linear-gradient(135deg,#000000,#ffffff)", pixels =>
            {
                // For a 200x100 box at 135deg, t=.5+(200*dx+100*dy)/300.
                // At the right-center axis t=5/6, whereas square UV geometry gives 3/4.
                AssertGray(pixels, 127, 64, 213, "right axis endpoint");
                AssertGray(pixels, 0, 64, 43, "left axis endpoint");
                AssertGray(pixels, 64, 0, 86, "top axis endpoint");
                AssertGray(pixels, 64, 127, 171, "bottom axis endpoint");
                AssertGray(pixels, 0, 0, 0, "starting corner");
                AssertGray(pixels, 127, 127, 255, "terminal corner");
            }, 200, 100);
        }

        private void WithRenderedGradient(string css, Action<Color32[]> assertions, float width=0, float height=0)
        {
            Texture2D gradient = null, readback = null;
            var target = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            var previousSrgb = GL.sRGBWrite;
            try
            {
                gradient = width > 0
                    ? (Texture2D)surface.GetType().GetMethod("CreateGradient", new[] { typeof(string), typeof(float), typeof(float) }).Invoke(surface, new object[] { css, width, height })
                    : (Texture2D)surface.GetType().GetMethod("CreateGradient", new[] { typeof(string) }).Invoke(surface, new object[] { css });
                target.Create(); GL.sRGBWrite = true;
                Graphics.Blit(gradient, target);
                RenderTexture.active = target;
                readback = new Texture2D(128, 128, TextureFormat.RGBA32, false);
                readback.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); readback.Apply();
                assertions(readback.GetPixels32());
            }
            finally
            {
                GL.sRGBWrite = previousSrgb; RenderTexture.active = previous;
                target.Release(); UnityEngine.Object.DestroyImmediate(target);
                if (readback != null) UnityEngine.Object.DestroyImmediate(readback);
                if (gradient != null) UnityEngine.Object.DestroyImmediate(gradient);
            }
        }

        private static void AssertGray(Color32[] pixels, int x, int fromTop, int expected, string location)
        {
            var color = pixels[(127 - fromTop) * 128 + x];
            Assert.That((int)color.r, Is.EqualTo(expected).Within(2), location + " red byte");
            Assert.That((int)color.g, Is.EqualTo(expected).Within(2), location + " green byte");
            Assert.That((int)color.b, Is.EqualTo(expected).Within(2), location + " blue byte");
            Assert.That(color.a, Is.EqualTo(255), location + " alpha byte");
        }

        private static Type Require(string name)
        {
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Galaxy101." + name)).FirstOrDefault(t => t != null);
            Assert.That(type, Is.Not.Null, "Missing native gradient dependency: " + name);
            return type;
        }
    }
}
