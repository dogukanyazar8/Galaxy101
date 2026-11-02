using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Galaxy101.Table;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Galaxy101.Tests
{
    public sealed class FinishRenderingTests
    {
        private GameObject owner;
        private RenderTexture target;
        private float oldScale;
        private string oldPlanet;

        [SetUp]
        public void SetUp()
        {
            oldScale = Time.timeScale; Time.timeScale = 1;
            oldPlanet = TableScene.PlanetOverride; TableScene.PlanetOverride = "Satürn";
        }

        [UnityTest]
        public IEnumerator EachFinishStyleReachesTheActualCameraFramebuffer()
        {
            string directory = Environment.GetEnvironmentVariable("GALAXY_HTML_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
            foreach (string style in new[] { "nuke", "blackhole", "supernova", "meteor" })
            {
                owner = new GameObject("Finish GPU " + style); owner.SetActive(false);
                var scene = owner.AddComponent<TableScene>();
                typeof(TableScene).GetField("demoLayout", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(scene, false);
                owner.SetActive(true);
                scene.SetRackSkin("walnut"); scene.SetTableSkin("station");
                target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                target.Create(); scene.RenderInto(target); scene.CameraRig.Camera.backgroundColor = Color.black;
                yield return null; yield return null;
                scene.CameraRig.enabled = false;
                var baseline = Read();
                var fx = owner.AddComponent<TableFx>(); fx.Init(scene);
                float start = Time.time; fx.FinishImpact(style, scene.CameraRig);
                // Includes the black-hole release and the supernova's delayed expansion.
                float sample = style == "blackhole" ? 1.3f : .7f;
                while (Time.time - start < sample) yield return null;
                yield return null;
                var image = Read();
                try
                {
                    var before = baseline.GetPixels32(); var after = image.GetPixels32();
                    int changed = 0, errorPixels = 0;
                    for (int i = 0; i < before.Length; i++)
                    {
                        if (Math.Abs(before[i].r - after[i].r) + Math.Abs(before[i].g - after[i].g) + Math.Abs(before[i].b - after[i].b) > 45) changed++;
                        if (after[i].r > 250 && after[i].g < 3 && after[i].b > 250) errorPixels++;
                    }
                    Assert.That(errorPixels, Is.LessThan(32), style + " rendered Unity's error shader.");
                    Assert.That(changed, Is.GreaterThan(1500), style + " created nodes but did not reach the rendered scene.");
                    TestContext.WriteLine("GALAXY101_FINISH_GPU style=" + style + " elapsed=" + (Time.time - start).ToString("F4", System.Globalization.CultureInfo.InvariantCulture) + " changedPixels=" + changed);
                    if (!string.IsNullOrEmpty(directory)) File.WriteAllBytes(Path.Combine(directory, "finish-" + style + ".png"), image.EncodeToPNG());
                }
                finally { UnityEngine.Object.Destroy(baseline); UnityEngine.Object.Destroy(image); }
                UnityEngine.Object.Destroy(owner); owner = null;
                yield return null;
                target.Release(); UnityEngine.Object.Destroy(target); target = null;
            }
            LogAssert.NoUnexpectedReceived();
        }

        private Texture2D Read()
        {
            var previous = RenderTexture.active;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            try { RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); image.Apply(); return image; }
            catch { UnityEngine.Object.Destroy(image); throw; }
            finally { RenderTexture.active = previous; }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (owner != null) UnityEngine.Object.Destroy(owner);
            yield return null;
            if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
            Time.timeScale = oldScale; TableScene.PlanetOverride = oldPlanet;
        }
    }
}
