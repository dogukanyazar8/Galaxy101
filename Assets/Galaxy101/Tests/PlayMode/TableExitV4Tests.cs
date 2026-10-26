using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Galaxy101.Table;
using NUnit.Framework;
using Okey101.Core.V4;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    public sealed class TableExitV4Tests
    {
        private GameObject gameOwner, sceneOwner, hudOwner;
        private string statePath;
        private TableHud hud;

        private TableGame Build(bool vsBots, long bet)
        {
            var table = TableGame.DefaultTable();
            table.VsBots = vsBots; table.Bet = bet;
            table.Id = vsBots ? "bots" : "exit-test";
            table.Name = vsBots ? "Botlara karşı" : "Test masası";
            TableGame.Pending = new TableLaunch
            {
                Setup = new TableSetup { Options = new MatchOptions { Mode = "solo", Hands = 5 }, Seed = 42, TurnMs = 60000 },
                Table = table, Flow = "play", Clock = () => Time.timeAsDouble * 1000,
            };
            gameOwner = new GameObject("Exit confirmation game");
            var game = gameOwner.AddComponent<TableGame>();
            sceneOwner = new GameObject("Exit confirmation scene"); sceneOwner.AddComponent<TableScene>();
            statePath = Path.Combine(Path.GetTempPath(), "g101-exit-v4-" + Guid.NewGuid().ToString("N") + ".json");
            hudOwner = new GameObject("Exit confirmation HUD"); hudOwner.SetActive(false);
            hudOwner.AddComponent<UIDocument>().panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            hud = hudOwner.AddComponent<TableHud>(); hudOwner.SetActive(true); hud.Initialize(statePath);
            hud.Settings.Set("lang", "tr");
            return game;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            TableGame.Pending = null;
            foreach (var owner in new[] { hudOwner, gameOwner, sceneOwner })
                if (owner != null) UnityEngine.Object.Destroy(owner);
            yield return null;
            if (statePath != null && File.Exists(statePath)) File.Delete(statePath);
        }

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator PaidFiveThousandTableWarnsAboutLosingItsStake() => CheckExit(false, 5000,
            "El bitmeden kalkarsan 5.000 bahsin kaybedilir ve yerine bot oynar.");

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator PaidTenThousandTableUsesItsActualStake() => CheckExit(false, 10000,
            "El bitmeden kalkarsan 10.000 bahsin kaybedilir ve yerine bot oynar.");

        [UnityTest]
        [Timeout(30000)]
        public IEnumerator BotTableShowsTheSourcePracticeWarning() => CheckExit(true, 0,
            "Maç bitmeden kalkarsan yerine bot oynar. Antrenman masasında bahis yoktur.");

        private IEnumerator CheckExit(bool vsBots, long bet, string expected)
        {
            var game = Build(vsBots, bet);
            yield return Until(() => game.HumanTurn && game.Model?.Table != null, "prepared play hand");
            Assert.That(game.Model.Table.VsBots, Is.EqualTo(vsBots), "The test must exercise the requested table mode.");
            Assert.That(game.Model.Table.Bet, Is.EqualTo(bet), "The displayed warning must use this table's actual stake.");
            game.RequestExit();
            yield return Until(() => hud.Canvas.Q<VisualElement>("table-exit-confirm")?.worldBound.width > 0, "visible exit card");
            var card = hud.Canvas.Q<VisualElement>("table-exit-confirm");
            Assert.That(card.layout.width, Is.EqualTo(518).Within(1), "CSS 460px content + 56px padding + 2px border");
            var visible = card.Query<TextElement>().ToList()
                .Where(e => e.worldBound.width > 0 && e.worldBound.height > 0 && e.resolvedStyle.display != DisplayStyle.None && e.resolvedStyle.visibility == Visibility.Visible)
                .Select(e => Regex.Replace(Regex.Replace(e.text ?? "", "<[^>]+>", ""), @"\s+", " ").Trim()).ToArray();
            CollectionAssert.Contains(visible, expected, "The exit warning must describe the selected table's real loss.");
        }

        private static IEnumerator Until(Func<bool> condition, string step)
        {
            float until = Time.realtimeSinceStartup + 10;
            while (!condition() && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(condition(), Is.True, "Timed out waiting for " + step);
        }
    }
}
