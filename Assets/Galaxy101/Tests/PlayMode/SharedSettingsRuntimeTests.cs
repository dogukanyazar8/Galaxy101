using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Galaxy101.Table;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    public sealed class SharedSettingsRuntimeTests
    {
        private GameObject sceneOwner, hudOwner;
        private string path;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (hudOwner != null) UnityEngine.Object.Destroy(hudOwner);
            if (sceneOwner != null) UnityEngine.Object.Destroy(sceneOwner);
            yield return null;
            if (path != null && File.Exists(path)) File.Delete(path);
        }

        [UnityTest]
        public IEnumerator ActualTableGearOpensTheCommonSixTabsAndChangesTheLiveTable()
        {
            path = Path.Combine(Path.GetTempPath(), "galaxy-shared-settings-" + Guid.NewGuid().ToString("N") + ".json");
            sceneOwner = new GameObject("Common settings scene"); sceneOwner.AddComponent<TableScene>();
            hudOwner = new GameObject("Common settings HUD"); hudOwner.SetActive(false);
            hudOwner.AddComponent<UIDocument>().panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            var hud = hudOwner.AddComponent<TableHud>(); hudOwner.SetActive(true); hud.Initialize(path);
            hud.Press("settings"); yield return null; yield return null;
            Assert.That(hud.Window, Is.EqualTo("shared-settings"));
            var root = hud.Canvas;
            Assert.That(root.Query<VisualElement>(className: "g-settings-nav").ToList().Count, Is.EqualTo(6));
            Assert.That(root.Q<Label>("menu.tabTitle").text, Is.EqualTo("Masa"));
            var mid = root.Q<VisualElement>("menu.v.pick[1]"); Assert.That(mid, Is.Not.Null);
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = mid; mid.SendEvent(submit); }
            yield return null;
            Assert.That(hud.Settings.View, Is.EqualTo("mid"), "The shared panel must write to the running table's own store.");
            var output = Environment.GetEnvironmentVariable("GALAXY_HTML_CAPTURE_DIR");
            if (!string.IsNullOrEmpty(output))
            {
                Directory.CreateDirectory(output);
                yield return DisplaySettingsTests.Shot(hudOwner.GetComponent<UIDocument>(), 1280, 720, Path.Combine(output, "table-shared-settings.png"));
                hud.CloseWindow(); hud.ToggleTwoDimensional(); yield return new WaitForSecondsRealtime(.4f);
                yield return DisplaySettingsTests.Shot(hudOwner.GetComponent<UIDocument>(), 1280, 720, Path.Combine(output, "table-2d.png"));
            }
            hud.CloseWindow(); yield return null;
            Assert.That(hud.Window, Is.Null);
        }

        [UnityTest]
        public IEnumerator SoundUpdatesPublishOnlyTheirChangedKey()
        {
            path = Path.Combine(Path.GetTempPath(), "galaxy-shared-delta-" + Guid.NewGuid().ToString("N") + ".json");
            sceneOwner = new GameObject("Common settings delta scene"); sceneOwner.AddComponent<TableScene>();
            hudOwner = new GameObject("Common settings delta HUD"); hudOwner.SetActive(false);
            hudOwner.AddComponent<UIDocument>().panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            var hud = hudOwner.AddComponent<TableHud>(); hudOwner.SetActive(true); hud.Initialize(path); yield return null;
            var changes = new List<string>(); hud.Settings.Changed += changes.Add;
            hud.Model.Settings["master"] = 37; hud.Model.NotifyChanged();
            CollectionAssert.AreEqual(new[] { "master" }, changes, "A sound change must not request the all-settings path that recreates every tile.");
            changes.Clear(); hud.Model.NotifyChanged();
            Assert.That(changes, Is.Empty, "An unchanged profile refresh must not restyle the table.");
        }
    }
}
