using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Galaxy101.Diagnostics;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    public sealed class CapturePreferencesTests
    {
        private G101StateRouter router;
        private string oldPath, oldServer, oldResolution;
        private bool? oldAuth;
        private GameDisplay.Mode oldMode;
        private int oldFrameRate, oldVsync;

        [UnityTest]
        public IEnumerator DisplayCaptureKeepsDefaultPreferencesInsideItsPhysicalWindow()
        {
            oldPath = GalaxyPreferencesBridge.OverridePath; oldServer = Online.GalaxyApi.Override;
            oldAuth = MainMenuController.AuthOnStart; oldMode = GameDisplay.CurrentMode; oldResolution = GameDisplay.Resolution;
            oldFrameRate = Application.targetFrameRate; oldVsync = QualitySettings.vSyncCount;
            string output = Path.Combine(Path.GetTempPath(), "g101-capture-preferences-" + Guid.NewGuid().ToString("N"));
            router = G101StateRouter.Run(new G101StateRouter.Options
            {
                States = new List<string> { "12" }, Width = 1280, Height = 720, Out = output
            });
            // Exercise the real menu path without the screenshot's WaitForEndOfFrame,
            // which does not resume in an Editor batch-mode test run.
            router.enabled = false;
            Directory.CreateDirectory(output);
            GalaxyPreferencesBridge.OverridePath = Path.Combine(output, "menu.json");
            var open = typeof(G101StateRouter).GetMethod("OpenMenu", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(open, Is.Not.Null);
            yield return (IEnumerator)open.Invoke(router, new object[] { "saturn", "tr", false });
            var menu = router.Menu;
            menu.View.OpenPanel("settings", 2);
            yield return null;
            Assert.That((string)menu.Model.Settings["wmode"], Is.EqualTo("fullscreen"), "Capture dimensions must not replace the HTML's default selected mode.");
            Assert.That((string)menu.Model.Settings["res"], Is.EqualTo(GameDisplay.Native), "The screenshot window must not replace the native-resolution preference.");
            Assert.That(menu.View.Root.Q<VisualElement>("setting-wmode-fullscreen").ClassListContains("selected"), Is.True);
            Assert.That(GameDisplay.LastRequest, Is.EqualTo((1280, 720, FullScreenMode.Windowed, 0d)), "The existing capture guard still controls the physical window.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (router != null) UnityEngine.Object.Destroy(router.gameObject);
            yield return null;
            var empty = SceneManager.CreateScene("After capture preference test " + Guid.NewGuid().ToString("N"));
            SceneManager.SetActiveScene(empty);
            var scene = SceneManager.GetSceneByName("Bootstrap");
            if (scene.IsValid() && scene.isLoaded)
            {
                var unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null) while (!unload.isDone) yield return null;
            }
            GalaxyPreferencesBridge.OverridePath = oldPath; Online.GalaxyApi.Override = oldServer;
            MainMenuController.AuthOnStart = oldAuth; GameClock.Reset(42);
            GameDisplay.Apply(GameDisplay.Key(oldMode), oldResolution);
            Application.targetFrameRate = oldFrameRate; QualitySettings.vSyncCount = oldVsync;
        }
    }
}
