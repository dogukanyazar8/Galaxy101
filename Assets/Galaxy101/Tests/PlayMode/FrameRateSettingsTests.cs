using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    /// <summary>Ayarlar → Görüntü → Kare hızı drives VSync / targetFrameRate at once and is kept.</summary>
    public sealed class FrameRateSettingsTests
    {
        private GameObject owner;
        private string statePath;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (owner != null) UnityEngine.Object.Destroy(owner);
            yield return null;
            FramePacing.Apply(FramePacing.Auto);
            if (statePath != null && File.Exists(statePath)) File.Delete(statePath);
        }

        private static void Click(VisualElement root, string name)
        {
            var e = root.Q<VisualElement>(name); Assert.That(e, Is.Not.Null, "element " + name);
            // Menu buttons use Clickable, which answers pointer and submit events (not a bare ClickEvent).
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = e; e.SendEvent(evt); }
        }

        [UnityTest]
        public IEnumerator FrameRateRowStepsThroughTheMonitorsRatesAndAppliesThem()
        {
            statePath = Path.Combine(Path.GetTempPath(), "galaxy-fps-" + Guid.NewGuid().ToString("N") + ".json");
            owner = new GameObject("Menu"); owner.SetActive(false);
            owner.AddComponent<UIDocument>().panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            var menu = owner.AddComponent<MainMenuController>(); owner.SetActive(true); menu.Initialize(statePath);
            yield return null;

            // Default: Otomatik = VSync to the monitor, no software cap (no screen pins 60 FPS any more).
            Assert.That(FramePacing.Choice, Is.EqualTo(FramePacing.Auto));
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(1)); Assert.That(Application.targetFrameRate, Is.EqualTo(-1));

            menu.View.OpenPanel("settings", 2); yield return null;
            var root = menu.View.Root;
            Assert.That(root.Q<Label>("setting-fps-value").text, Does.StartWith(menu.View.L("Otomatik", "Auto")));
            Assert.That(root.Q<VisualElement>("setting-fps-prev").ClassListContains("at-end"), Is.True, "Otomatik is the first choice");

            Click(root, "setting-fps-next"); yield return null;
            Assert.That((int)menu.Model.Settings["fps"], Is.EqualTo(30));
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(0)); Assert.That(Application.targetFrameRate, Is.EqualTo(30));
            Assert.That(root.Q<Label>("setting-fps-value").text, Is.EqualTo("30 FPS"));

            // Last choice: the monitor's own maximum, VSync off.
            var options = FramePacing.Options(FramePacing.MaxHz, FramePacing.IsMobile);
            for (int i = 1; i < options.Count; i++) { Click(root, "setting-fps-next"); yield return null; }
            Assert.That((int)menu.Model.Settings["fps"], Is.EqualTo(options[options.Count - 1]));
            Assert.That(root.Q<VisualElement>("setting-fps-next").ClassListContains("at-end"), Is.True);
            Click(root, "setting-fps-next"); yield return null;
            Assert.That((int)menu.Model.Settings["fps"], Is.EqualTo(options[options.Count - 1]), "nothing past the last choice");
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(0)); Assert.That(Application.targetFrameRate, Is.EqualTo(options[options.Count - 1]));

            string dir = Environment.GetEnvironmentVariable("GALAXY_FPS_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(dir)) yield return Capture(owner.GetComponent<UIDocument>(), Path.Combine(dir, "settings_frame_rate.png"));

            // Kept across launches (the save file holds the choice).
            Assert.That(File.ReadAllText(statePath), Does.Match(@"""fps"":\s*" +options[options.Count - 1]));
        }

        private static IEnumerator Capture(UIDocument document, string path)
        {
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var panel = UnityEngine.Object.Instantiate(document.panelSettings); var original = document.panelSettings;
            panel.targetTexture = target; document.panelSettings = panel;
            for (int i = 0; i < 20; i++) yield return null;
            yield return new WaitForSecondsRealtime(1f);
            var previous = RenderTexture.active; RenderTexture.active = target;
            var pixels = new Texture2D(1280, 720, TextureFormat.RGBA32, false); pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            RenderTexture.active = previous; File.WriteAllBytes(path, pixels.EncodeToPNG());
            document.panelSettings = original; UnityEngine.Object.Destroy(panel); target.Release(); UnityEngine.Object.Destroy(target); UnityEngine.Object.Destroy(pixels);
        }
    }
}
