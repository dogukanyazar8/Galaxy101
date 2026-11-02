using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Galaxy101.Table;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Galaxy101.Tests
{
    public sealed class TableBackdropRuntimeTests
    {
        private GameObject sceneOwner, hudOwner;
        private UIDocument document;
        private PanelSettings previousPanel, capturePanel;
        private RenderTexture composite;
        private string statePath, previousPlanet;
        private readonly List<Texture2D> readbacks = new List<Texture2D>();
        private static readonly Vector2[] WindowPoints = { new Vector2(220, 100), new Vector2(260, 110), new Vector2(300, 100) };

        [UnityTest]
        public IEnumerator SaturnRemainsVisibleThroughTheSeparateShipAndTableFramebuffers()
        {
            previousPlanet = TableScene.PlanetOverride;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) Assert.Ignore("A graphics device is required to validate table backdrop compositing.");
            TableScene.PlanetOverride = "Satürn";
            statePath = Path.Combine(Path.GetTempPath(), "galaxy-backdrop-" + Guid.NewGuid().ToString("N") + ".json");
            sceneOwner = new GameObject("Backdrop table scene"); var table = sceneOwner.AddComponent<TableScene>();
            hudOwner = new GameObject("Backdrop table HUD"); hudOwner.SetActive(false);
            document = hudOwner.AddComponent<UIDocument>(); document.panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            var hud = hudOwner.AddComponent<TableHud>(); hudOwner.SetActive(true); hud.Initialize(statePath);
            hud.Settings.Set("view", "seat"); hud.Settings.Set("motion", false);
            hud.Render(new TableHud.State { Phase = "opp", Active = 1 });
            previousPanel = document.panelSettings; capturePanel = Object.Instantiate(previousPanel);
            composite = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Assert.That(composite.Create(), Is.True); capturePanel.targetTexture = composite; document.panelSettings = capturePanel;

            var background = hudOwner.GetComponentInChildren<GalaxyBackground>();
            var ships = hudOwner.GetComponentInChildren<GalaxyShips>();
            Assert.That(background, Is.Not.Null); Assert.That(ships, Is.Not.Null);
            // A native capture can initialize the table while its window lacks OS focus. The first static image
            // must still exist; focus only suspends animation. Set this before the first Update, including in batch.
            double pausedAt = background.TimeState.Seconds;
            background.SetFocused(false);
            float deadline = Time.realtimeSinceStartup + 20;
            while (!ships.Initialization.IsCompleted && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(ships.IsReady, Is.True, "The actual transparent ship layer must have completed initialization.");
            string directory = Environment.GetEnvironmentVariable("GALAXY_TABLE_BACKDROP_CAPTURE_DIR");
            if (string.IsNullOrEmpty(directory)) directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../artifacts/table-backdrop-runtime"));
            Directory.CreateDirectory(directory);
            TestContext.WriteLine("TABLE_BACKDROP_ARTIFACTS " + directory);
            yield return null; yield return null; yield return null;
            bool automaticBackground = background.Output != null;
            TestContext.WriteLine($"TABLE_BACKDROP_AUTOMATIC backgroundCreated={automaticBackground}, applicationFocused={Application.isFocused}, batch={Application.isBatchMode}, backgroundBounds={hud.Canvas.Q<VisualElement>("table-background").worldBound}");
            if (automaticBackground) Capture(background.Output, directory, "00-automatic-background");
            Capture(composite, directory, "00-automatic-composite");
            background.SetFocused(false); ships.SetFocused(false);
            background.RenderOnce(1280, 720); ships.RenderOnce(1280, 720);
            yield return null; yield return null; yield return null;

            var camera = table.CameraRig.Camera;
            var view = hud.Canvas.Q<VisualElement>("table-3d");
            var shipLayer = hud.Canvas.Q<VisualElement>("table.ships");
            TestContext.WriteLine($"TABLE_BACKDROP_FORMAT background={background.Output.graphicsFormat}, ships={ships.Output.graphicsFormat}, table={camera.targetTexture.graphicsFormat}, tableMSAA={camera.targetTexture.antiAliasing}, UI={composite.graphicsFormat}");
            var planetPixels = Capture(background.Output, directory, "01-background");
            var shipPixels = Capture(ships.Output, directory, "02-ships");
            var tablePixels = Capture(camera.targetTexture, directory, "03-table");
            var actual = Capture(composite, directory, "04-composite");

            view.style.opacity = 0;
            yield return null; yield return null;
            Capture(composite, directory, "05-without-table");
            shipLayer.style.opacity = 0;
            yield return null; yield return null;
            var unobstructed = Capture(composite, directory, "06-background-and-hud");
            view.style.opacity = shipLayer.style.opacity = 1;

            // These bounded diagnostics isolate the clear, scene geometry, custom tone pass and URP post stack.
            // They do not alter the expectations: a clear pixel in the real table target must remain transparent.
            var tone = camera.GetComponent<ThreeToneMapping>(); var cameraData = camera.GetUniversalAdditionalCameraData();
            bool oldTone = tone.enabled, oldPost = cameraData.renderPostProcessing; int oldMask = camera.cullingMask;
            tone.enabled = false;
            yield return null; yield return null;
            Capture(camera.targetTexture, directory, "07-table-without-three-tone");
            cameraData.renderPostProcessing = false;
            yield return null; yield return null;
            Capture(camera.targetTexture, directory, "08-table-without-post");
            camera.cullingMask = 0;
            yield return null; yield return null;
            Capture(camera.targetTexture, directory, "09-clear-camera");
            camera.cullingMask = oldMask; cameraData.renderPostProcessing = oldPost; tone.enabled = oldTone;

            Assert.That(automaticBackground, Is.True, "A visible table must produce its initial background without an explicit diagnostic RenderOnce.");
            Assert.That(background.TimeState.Seconds, Is.EqualTo(pausedAt).Within(1e-9), "An unfocused backdrop must not advance its animation clock.");
            foreach (var point in WindowPoints)
            {
                var planet = Pixel(planetPixels, point); var ship = Pixel(shipPixels, point); var room = Pixel(tablePixels, point);
                var drawn = Pixel(actual, point); var wanted = Pixel(unobstructed, point);
                Assert.That(planet.r + planet.g + planet.b, Is.GreaterThan(.6f), "The raw backdrop must contain Saturn at " + point);
                Assert.That(planet.r, Is.GreaterThan(planet.b + .08f), "The window sample must be on Saturn, not its navy sky, at " + point);
                Assert.That(ship.a, Is.LessThan(.02f), "The ship target must leave the planet clear at " + point);
                Assert.That(room.a, Is.LessThan(.02f), "The table target must leave its window transparent at " + point);
                Assert.That(drawn.r, Is.EqualTo(wanted.r).Within(.03f), "Saturn red survives the actual table composite at " + point);
                Assert.That(drawn.g, Is.EqualTo(wanted.g).Within(.03f), "Saturn green survives the actual table composite at " + point);
                Assert.That(drawn.b, Is.EqualTo(wanted.b).Within(.03f), "Saturn blue survives the actual table composite at " + point);
            }

            // A marker would be overwritten by any background draw. A clean paused target must retain it, while a
            // dirty theme change must replace it once without resuming the clock. This proves the focus exception
            // does not turn an unfocused table into a continuously rendering background.
            Clear(background.Output, Color.magenta);
            yield return null; yield return null; yield return null;
            var clean = Capture(background.Output, directory, "10-clean-paused-marker");
            foreach (var point in WindowPoints)
            {
                var marker = Pixel(clean, point);
                Assert.That(marker.r, Is.EqualTo(1).Within(.005f)); Assert.That(marker.g, Is.LessThan(.005f)); Assert.That(marker.b, Is.EqualTo(1).Within(.005f));
            }
            background.Configure("Mars", true, "Yüksek");
            yield return null; yield return null; yield return null;
            var changed = Capture(background.Output, directory, "11-unfocused-dirty-theme");
            foreach (var point in WindowPoints)
            {
                var mars = Pixel(changed, point);
                Assert.That(mars.r, Is.GreaterThan(mars.g + .03f), "A dirty Mars theme must replace the paused marker at " + point);
                Assert.That(mars.r, Is.GreaterThan(mars.b + .03f), "A dirty Mars theme must replace the paused marker at " + point);
            }
            Assert.That(background.TimeState.Seconds, Is.EqualTo(pausedAt).Within(1e-9), "A dirty theme repaint must not resume the paused animation clock.");
        }

        private static void Clear(RenderTexture target, Color color)
        {
            var previous = RenderTexture.active;
            try { RenderTexture.active = target; GL.Clear(false, true, color); }
            finally { RenderTexture.active = previous; }
        }

        private Texture2D Capture(RenderTexture target, string directory, string name)
        {
            var previous = RenderTexture.active; var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            readbacks.Add(pixels);
            try
            {
                RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
                File.WriteAllBytes(Path.Combine(directory, name + ".png"), pixels.EncodeToPNG());
                foreach (var point in WindowPoints) TestContext.WriteLine($"TABLE_BACKDROP {name} at={point} rgba={Pixel(pixels, point)}");
                return pixels;
            }
            finally { RenderTexture.active = previous; }
        }

        private static Color Pixel(Texture2D pixels, Vector2 point)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt(point.x / 1280 * pixels.width), 0, pixels.width - 1);
            int y = Mathf.Clamp(pixels.height - 1 - Mathf.FloorToInt(point.y / 720 * pixels.height), 0, pixels.height - 1);
            return pixels.GetPixel(x, y);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            TableScene.PlanetOverride = previousPlanet;
            if (document != null && previousPanel != null) document.panelSettings = previousPanel;
            if (capturePanel != null) capturePanel.targetTexture = null;
            if (hudOwner != null) Object.Destroy(hudOwner);
            if (sceneOwner != null) Object.Destroy(sceneOwner);
            if (capturePanel != null) Object.Destroy(capturePanel);
            if (composite != null) { composite.Release(); Object.Destroy(composite); }
            foreach (var pixels in readbacks) if (pixels != null) Object.Destroy(pixels);
            readbacks.Clear();
            yield return null;
            if (statePath != null && File.Exists(statePath)) File.Delete(statePath);
        }
    }
}
