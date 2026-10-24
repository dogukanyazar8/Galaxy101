using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    /// <summary>
    /// Every main-menu window laid out for tools/compare_ui.py against tools/ref_menu_capture.py (only with
    /// GALAXY_UI_DUMP_DIR; otherwise it only checks that each window opens).
    /// </summary>
    public sealed class MenuWindowDumpTests
    {
        private GameObject owner;
        private string statePath;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            MainMenuController.AuthOnStart = null;
            if (owner != null) UnityEngine.Object.Destroy(owner);
            yield return null;
            if (statePath != null && File.Exists(statePath)) File.Delete(statePath);
        }

        [UnityTest]
        public IEnumerator EveryMenuWindowOpensAndIsMeasured()
        {
            statePath = Path.Combine(Path.GetTempPath(), "galaxy-windows-" + Guid.NewGuid().ToString("N") + ".json");
            MainMenuController.AuthOnStart = false;
            owner = new GameObject("Menu windows"); owner.SetActive(false);
            owner.AddComponent<UIDocument>().panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            var menu = owner.AddComponent<MainMenuController>(); owner.SetActive(true); menu.Initialize(statePath);
            yield return null; yield return null;
            var view = menu.View; var root = view.Root;
            // The guest greeting as the shell flashes it (tools/ref_capture.py S06_menu carries the same pill).
            view.Flash("Hoş geldin, Misafir9369! 10.000 çip hesabına eklendi", "gold"); yield return new WaitForSeconds(.8f);
            Assert.That(root.Q<VisualElement>("shell-flash"), Is.Not.Null);
            yield return UiDump.Capture(root, "M00_flash");
            foreach (var (step, key) in new[] { ("M01_settings", "settings"), ("M02_shop", "shop"), ("M03_friends", "friends"), ("M04_leader", "leader"), ("M05_daily", "daily"), ("M06_rules", "rules"), ("M07_profile", "me") })
            {
                view.OpenPanel(key, 0); yield return new WaitForSeconds(.5f);
                Assert.That(view.Modals.Panel, Is.EqualTo(key));
                yield return UiDump.Capture(root, step);
                view.CloseTop(); yield return new WaitForSeconds(.3f);
            }
        }
    }
}
