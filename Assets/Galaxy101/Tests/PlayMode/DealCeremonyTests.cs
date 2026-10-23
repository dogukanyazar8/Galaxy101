using System;
using System.Collections;
using System.IO;
using System.Linq;
using Galaxy101.Table;
using NUnit.Framework;
using Okey101.Core.Turns;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    /// <summary>
    /// spec/21 deal ceremony on the live table: 15 × 7 stacks, my two rolls, the indicator turned up, the stacks handed out
    /// and every hand on its rack, then the board takes over with the same tiles where the ceremony left them.
    /// GALAXY_DEAL_CAPTURE_DIR saves a frame per step (G02–G12).
    /// </summary>
    public sealed class DealCeremonyTests
    {
        private GameObject gameOwner, sceneOwner, hudOwner;
        private string statePath;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1;
            foreach (var go in new[] { hudOwner, gameOwner, sceneOwner }) if (go != null) UnityEngine.Object.Destroy(go);
            yield return null;
            if (statePath != null && File.Exists(statePath)) File.Delete(statePath);
        }

        private static IEnumerator Until(Func<bool> condition, float seconds, string what)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (!condition()) { if (Time.realtimeSinceStartup > end) Assert.Fail("Timed out waiting for " + what); yield return null; }
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator IDealRollTwiceAndEveryHandLandsOnItsRack()
        {
            // Starting player 1 (my right) means I deal.
            var state = RoundFactory.Create(4242, Guid.NewGuid(), 1);
            gameOwner = new GameObject("TableGame"); var game = gameOwner.AddComponent<TableGame>();
            game.Configure(TableGame.Me, 4242, 60, false, state); game.CeremonyOnPreparedHand = true;
            sceneOwner = new GameObject("GalaxyTable"); sceneOwner.AddComponent<TableScene>();
            statePath = Path.Combine(Path.GetTempPath(), "galaxy-deal-" + Guid.NewGuid().ToString("N") + ".json");
            hudOwner = new GameObject("TableHud"); hudOwner.SetActive(false);
            hudOwner.AddComponent<UIDocument>().panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            var hud = hudOwner.AddComponent<TableHud>(); hudOwner.SetActive(true); hud.Initialize(statePath);
            string dir = Environment.GetEnvironmentVariable("GALAXY_DEAL_CAPTURE_DIR");
            bool capture = !string.IsNullOrEmpty(dir);
            if (!capture) Time.timeScale = 3;

            yield return Until(() => game.Ceremony != null && game.Ceremony.Step == "stack", 20, "the stacks");
            if (capture) { yield return new WaitForSeconds(1.2f); yield return TableGameRuntimeTests.Capture(hudOwner.GetComponent<UIDocument>(), Path.Combine(dir, "G02_stacks.png")); }
            yield return Until(() => game.HudState().Deal?.Roll == 0, 20, "my Zar at button");
            Assert.That(game.HudState().Deal.Dealer, Is.EqualTo(TableGame.Me));
            yield return null;
            Assert.That(hud.Canvas.Q<VisualElement>("deal-roll"), Is.Not.Null);
            if (capture) yield return TableGameRuntimeTests.Capture(hudOwner.GetComponent<UIDocument>(), Path.Combine(dir, "G04_roll_button.png"));
            hud.Press("roll");
            yield return Until(() => game.Ceremony.Step == "rolling", 5, "the first roll");
            if (capture) { yield return new WaitForSeconds(.5f); yield return TableGameRuntimeTests.Capture(hudOwner.GetComponent<UIDocument>(), Path.Combine(dir, "G05_rolling.png")); }
            yield return Until(() => game.HudState().Deal?.Roll == 1, 10, "Tekrar at");
            Assert.That(game.Ceremony.Dice[0], Is.InRange(1, 6));
            if (capture) yield return TableGameRuntimeTests.Capture(hudOwner.GetComponent<UIDocument>(), Path.Combine(dir, "G06_roll_again.png"));
            hud.Press("roll");
            yield return Until(() => game.Ceremony.Revealed, 15, "the indicator");
            if (capture) { yield return new WaitForSeconds(.3f); yield return TableGameRuntimeTests.Capture(hudOwner.GetComponent<UIDocument>(), Path.Combine(dir, "G07_indicator.png")); }
            yield return Until(() => game.Ceremony.Step == "deal", 15, "the hand-out");
            if (capture) { yield return new WaitForSeconds(2.2f); yield return TableGameRuntimeTests.Capture(hudOwner.GetComponent<UIDocument>(), Path.Combine(dir, "G09_dealing.png")); }
            yield return Until(() => game.Ceremony.Step == "fill", 20, "filling the racks");
            if (capture) { yield return new WaitForSeconds(.5f); yield return TableGameRuntimeTests.Capture(hudOwner.GetComponent<UIDocument>(), Path.Combine(dir, "G11_filling.png")); }
            yield return Until(() => game.Current != TableGame.Phase.Deal, 30, "the first turn");

            var view = game.Session.GetView(TableGame.Me);
            Assert.That(game.Board.ViewOf(view.OwnHand.First().Value), Is.Not.Null, "my tiles are board objects now");
            Assert.That(game.Rack.Count, Is.EqualTo(view.OwnHand.Count));
            for (int k = 1; k < 4; k++) Assert.That(game.Board.HiddenCount(k), Is.EqualTo(view.Players[k].HandCount), "seat " + k);
            Assert.That(view.Players[1].HandCount + (game.Active == 1 ? 0 : 0), Is.EqualTo(22).Or.EqualTo(21), "my right starts with 22");
            Assert.That(game.Board.DeckShown, Is.EqualTo(view.StockCount));
            Assert.That(GameObject.Find("toren_tas"), Is.Null, "the ceremony's own tiles are gone");
            if (capture) { yield return new WaitForSeconds(1.5f); yield return TableGameRuntimeTests.Capture(hudOwner.GetComponent<UIDocument>(), Path.Combine(dir, "G12_started.png")); }
        }
    }
}
