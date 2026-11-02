using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    public sealed class RingProfileTests
    {
        private static Type Profile => AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Galaxy101.RingProfileTexture")).FirstOrDefault(t => t != null);

        // Independent JavaScript fixtures from the active planet-renderer.js ringAt / n1 / h1 functions.
        [TestCase(1.30, 0, 0, 0, 0)]
        [TestCase(1.45, 0, 0, 0, 0)]
        [TestCase(1.49, .6161910246, .5751116230, .5093845804, .1090859764)]
        [TestCase(1.53, .8867694291, .8038937815, .6298549216, .7125149185)]
        [TestCase(1.65, .9739896387, .8765599358, .6523748345, .8735414177)]
        [TestCase(1.90, .8815510457, .7963434220, .6384588290, .8084192170)]
        [TestCase(2.01, .4229534109, .3947565169, .3524611758, .0456238615)]
        [TestCase(2.21, .7239793460, .6722665355, .5762284590, .0521462677)]
        [TestCase(2.30, 0, 0, 0, 0)]
        [TestCase(2.32, .8161918617, .7773255826, .6995930244, .3385322248)]
        [TestCase(2.34, 0, 0, 0, 0)]
        public void ProfileMatchesOriginalJavascriptDoublePrecisionFixtures(double radius, double red, double green, double blue, double alpha)
        {
            Assert.That(Profile, Is.Not.Null);
            var actual = (Color)Profile.GetMethod("Evaluate").Invoke(null, new object[] { radius });
            Assert.That(actual.r, Is.EqualTo(red).Within(.000001));
            Assert.That(actual.g, Is.EqualTo(green).Within(.000001));
            Assert.That(actual.b, Is.EqualTo(blue).Within(.000001));
            Assert.That(actual.a, Is.EqualTo(alpha).Within(.000001));
        }

        [Test]
        public void BackgroundsShareOneProfileUntilTheirLastConsumerIsDestroyed()
        {
            int before = Resources.FindObjectsOfTypeAll<Texture2D>().Count(t => t.name == "Galaxy101 shared ring profile");
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Galaxy101.GalaxyBackground")).First(t => t != null);
            var first = new GameObject("First ring profile consumer"); var second = new GameObject("Second ring profile consumer");
            try
            {
                var a = first.AddComponent(type); var b = second.AddComponent(type);
                type.GetMethod("Initialize").Invoke(a, new object[] { new VisualElement() });
                type.GetMethod("Initialize").Invoke(b, new object[] { new VisualElement() });
                var field = type.GetField("ringProfile", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null);
                var texture = (Texture2D)field.GetValue(a);
                Assert.That(texture, Is.Not.Null); Assert.That(field.GetValue(b), Is.SameAs(texture));
                Assert.That(texture.filterMode, Is.EqualTo(FilterMode.Trilinear));
                Assert.That(texture.mipmapCount, Is.GreaterThan(1));
                UnityEngine.Object.DestroyImmediate(first); first = null;
                Assert.That(texture != null, Is.True, "Destroying one background must not invalidate another background's ring texture.");
            }
            finally { if (first != null) UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second); }
            Assert.That(Resources.FindObjectsOfTypeAll<Texture2D>().Count(t => t.name == "Galaxy101 shared ring profile"), Is.EqualTo(before));
        }

        [Test]
        public void RemovedInnerCRingDoesNotReachTheFramebuffer()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("A graphics device is required for the ring framebuffer regression.");
            var target = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var mesh = new Mesh(); var material = new Material(Resources.Load<Shader>("Galaxy101Planet"));
            var commands = new CommandBuffer(); Texture2D pixels = null, profile = null; var previous = RenderTexture.active;
            try
            {
                target.Create();
                mesh.vertices = new[] { new Vector3(-360, -150), new Vector3(360, -150), new Vector3(360, 150), new Vector3(-360, 150) };
                mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                material.SetTexture("_CanvasBackdrop", Texture2D.blackTexture); material.SetTexture("_Surface", Texture2D.blackTexture);
                material.SetVector("_Placement", new Vector4(640, 360, 1, 0)); material.SetVector("_Planet", new Vector4(22 * Mathf.Deg2Rad, 1.1f, .45f, .02f));
                material.SetFloat("_Rings", 1); material.SetFloat("_PixelScale", 1);
                if (Profile != null) { profile = (Texture2D)Profile.GetMethod("Acquire").Invoke(null, null); material.SetTexture("_RingProfile", profile); }
                commands.SetRenderTarget(target); commands.SetViewport(new Rect(0, 0, 1280, 720)); commands.ClearRenderTarget(false, true, Color.black); commands.DrawMesh(mesh, Matrix4x4.identity, material);
                Graphics.ExecuteCommandBuffer(commands); RenderTexture.active = target;
                pixels = new Texture2D(1280, 720, TextureFormat.RGBA32, false); pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
                // r=1.28..1.42 lies outside the planet, inside the removed C ring, away from the retained soft B-ring edge.
                for (int x = 831; x <= 850; x++)
                {
                    var sample = pixels.GetPixel(x, 360);
                    Assert.That(Mathf.Max(sample.r, sample.g, sample.b), Is.LessThan(1f / 255), "Removed C ring still rendered at canvas x=" + x);
                }
                Assert.That(pixels.GetPixel(900, 360).r, Is.GreaterThan(.15f), "The retained B ring must still render.");
            }
            finally
            {
                RenderTexture.active = previous; commands.Release(); target.Release();
                UnityEngine.Object.DestroyImmediate(material); UnityEngine.Object.DestroyImmediate(mesh); UnityEngine.Object.DestroyImmediate(target);
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                if (profile != null) Profile.GetMethod("Release").Invoke(null, new object[] { profile });
            }
        }
    }
}
