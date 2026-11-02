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
    public sealed class TableFlatEffectsTests
    {
        private GameObject sceneOwner, hudOwner;
        private string path;
        private TableHud hud;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameClock.Reset(42);
            sceneOwner = new GameObject("Flat effects scene"); sceneOwner.AddComponent<TableScene>();
            path = Path.Combine(Path.GetTempPath(), "g101-flat-effects-" + Guid.NewGuid().ToString("N") + ".json");
            hudOwner = new GameObject("Flat effects HUD"); hudOwner.SetActive(false);
            hudOwner.AddComponent<UIDocument>().panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            hud = hudOwner.AddComponent<TableHud>(); hudOwner.SetActive(true); hud.Initialize(path);
            hud.Model.Settings["motion"] = true; hud.Settings.Set("view", "2d");
            yield return null; yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (hudOwner != null) UnityEngine.Object.Destroy(hudOwner);
            if (sceneOwner != null) UnityEngine.Object.Destroy(sceneOwner);
            yield return null;
            if (path != null && File.Exists(path)) File.Delete(path);
            GameClock.Reset(42);
        }

        [UnityTest]
        [Timeout(15000)]
        public IEnumerator FlatGiftArrivesOnceAndLeavingTheViewCancelsAnotherFlight()
        {
            string kind = hud.Gifts[0].Key;
            hud.FlatGift(kind, 1, 0);
            Assert.That(hud.Canvas.Q<VisualElement>("flat-fx-gift"), Is.Not.Null);
            float until = Time.realtimeSinceStartup + 6;
            while (hud.Received.Count == 0 && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(hud.Received.Count, Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(1.4f);
            Assert.That(hud.Received.Count, Is.EqualTo(1), "A visual completion must never duplicate the landing notification.");
            hud.FlatGift(kind, 1, 0); hud.ToggleTwoDimensional();
            Assert.That(hud.Canvas.Q<VisualElement>("table-flat-effects").childCount, Is.EqualTo(0));
            yield return new WaitForSecondsRealtime(3);
            Assert.That(hud.Received.Count, Is.EqualTo(1), "A cancelled view cannot fire a delayed landing.");
        }

        [UnityTest]
        public IEnumerator BackgroundMotionPreferenceDoesNotDisableDiceOrGiftEffects()
        {
            hud.Model.Settings["motion"] = false;
            hud.FlatRollDie(2, 6);
            Assert.That(hud.Canvas.Q<VisualElement>("flat-fx-die").childCount, Is.EqualTo(6));
            hud.FlatGift(hud.Gifts[0].Key, 1, 0);
            Assert.That(hud.Received.Count, Is.EqualTo(0), "The background switch must not skip the source gift flight.");
            Assert.That(hud.Canvas.Q<VisualElement>("flat-fx-gift"), Is.Not.Null);
            float until = Time.realtimeSinceStartup + 6;
            while (hud.Received.Count == 0 && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(hud.Received.Count, Is.EqualTo(1), "Disabling background motion must not freeze a gift's landing cue.");
        }

        [UnityTest]
        [Timeout(20000)]
        public IEnumerator EveryFinishStyleCreatesItsNativeEffectAndClearsOnViewExit()
        {
            hud.Model.Settings["motion"] = false;
            string[] styles = { "nuke", "blackhole", "supernova", "meteor" };
            string[] nodes = { "flat-fx-glow", "flat-fx-blackhole", "flat-fx-ray", "flat-fx-meteor" };
            for (int i = 0; i < styles.Length; i++)
            {
                hud.Settings.Set("bombFx", styles[i]);
                Assert.That(hud.FlatFinish(i, styles[i]), Is.EqualTo(1.75f));
                Assert.That(hud.Canvas.Q<VisualElement>("flat-fx-finish-tile"), Is.Not.Null);
                yield return new WaitForSecondsRealtime(1.85f);
                Assert.That(hud.Canvas.Q<VisualElement>(nodes[i]), Is.Not.Null, styles[i]);
                hud.ToggleTwoDimensional();
                Assert.That(hud.Canvas.Q<VisualElement>("table-flat-effects").childCount, Is.EqualTo(0));
                hud.ToggleTwoDimensional(); yield return null;
            }
        }
    }
}
