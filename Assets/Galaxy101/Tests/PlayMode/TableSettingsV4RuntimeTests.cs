using System;
using System.Collections;
using System.IO;
using Galaxy101.Table;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    public sealed class TableSettingsV4RuntimeTests
    {
        private GameObject sceneOwner, hudOwner;
        private string statePath;
        private bool designDefaults;
        private TableHud hud;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            designDefaults = TableSettings.DesignDefaultsOnly; TableSettings.DesignDefaultsOnly = true;
            statePath = Path.Combine(Path.GetTempPath(), "galaxy-settings-v4-" + Guid.NewGuid().ToString("N") + ".json");
            sceneOwner = new GameObject("Table settings v4 scene"); sceneOwner.AddComponent<TableScene>();
            hudOwner = new GameObject("Table settings v4 HUD"); hudOwner.SetActive(false);
            hudOwner.AddComponent<UIDocument>().panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            hud = hudOwner.AddComponent<TableHud>(); hudOwner.SetActive(true); hud.Initialize(statePath);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            TableSettings.DesignDefaultsOnly = designDefaults;
            if (hudOwner != null) UnityEngine.Object.Destroy(hudOwner);
            if (sceneOwner != null) UnityEngine.Object.Destroy(sceneOwner);
            yield return null;
            if (File.Exists(statePath)) File.Delete(statePath);
        }

        private static void Click(VisualElement element)
        {
            Assert.That(element, Is.Not.Null);
            using (var click = ClickEvent.GetPooled()) { click.target = element; element.SendEvent(click); }
        }

        [UnityTest]
        public IEnumerator CameraCardsSelectAllThreePackageViewsAndKeepFpsAfterThePackageSections()
        {
            hud.OpenSettings(2); yield return new WaitForSecondsRealtime(.5f);
            var root = hud.Canvas;
            foreach (var id in new[] { "table.t.setTitle", "table.setHead.title", "table.setHead.sub", "table.t.lkView", "table.t.lkTable", "table.t.lkRack", "table.t.lkTile", "table.resetSettings", "table.closeSettings#2", "table.closeSettings#3" })
                Assert.That(root.Q<VisualElement>(id), Is.Not.Null, id);
            Assert.That(root.Q<Label>("table.v.name[1]").text, Does.Contain("Yarı yüksek"));
            Assert.That(root.Q<VisualElement>("table.v.pick[2]"), Is.Not.Null);
            Assert.That(root.Q<VisualElement>("setting-row-fps").worldBound.yMin, Is.GreaterThan(root.Q<VisualElement>("table.t.lkTile").worldBound.yMax));
            Assert.That(root.Q<VisualElement>("setting-row-fps").worldBound.yMin, Is.GreaterThan(root.Q<VisualElement>("table-settings-list").worldBound.yMax), "The existing FPS preference must not shift G32/T15's initial package viewport.");
            Click(root.Q<VisualElement>("table.v.pick[1]")); yield return null;
            Assert.That(hud.Settings.View, Is.EqualTo("mid")); Assert.That(hud.PreferredView, Is.EqualTo(TableCameraRig.View.Mid));
            Click(root.Q<VisualElement>("table.v.pick[2]")); yield return null;
            Assert.That(hud.Settings.View, Is.EqualTo("top")); Assert.That(hud.PreferredView, Is.EqualTo(TableCameraRig.View.Top));
        }

        [UnityTest]
        public IEnumerator GameSoundAndAlertTabsKeepTheirIndexedControlsAndApplyWithoutRebuildingThem()
        {
            foreach (var tab in new[] { 0, 1, 3 })
            {
                hud.OpenSettings(tab); yield return new WaitForSecondsRealtime(.35f);
                var root = hud.Canvas;
                var description = root.Q<Label>("table.row.desc[0]");
                Assert.That(description.resolvedStyle.fontSize, Is.EqualTo(12.5f));
                var reset = root.Q<Label>("table.resetSettings");
                Assert.That(reset.resolvedStyle.whiteSpace, Is.EqualTo(WhiteSpace.NoWrap));
                Assert.That(reset.worldBound.height, Is.EqualTo(34).Within(1), "The reset action is one line plus 8 px vertical padding.");
                int toggleIndex = tab == 1 ? 3 : 0;
                var toggle = root.Q<VisualElement>("table.row.toggle[" + toggleIndex + "]");
                string key = tab == 0 ? "autoSort" : tab == 1 ? "tileSnd" : "bubbles";
                bool before = hud.Settings.Bool(key); Click(toggle); yield return null;
                Assert.That(hud.Settings.Bool(key), Is.EqualTo(!before));
                Assert.That(root.Q<VisualElement>(toggle.name), Is.SameAs(toggle), "A settings change must not interrupt a live control.");
                if (tab == 1) Assert.That(root.Q<GalaxySlider>("table.row.input[0]"), Is.Not.Null);
            }
        }
    }
}
