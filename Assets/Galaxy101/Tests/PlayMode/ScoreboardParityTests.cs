using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Galaxy101.Table;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    public sealed class ScoreboardParityTests
    {
        private GameObject sceneOwner, hudOwner;
        private PanelSettings panel;
        private RenderTexture target;
        private string path;
        private TableHud hud;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            path = Path.Combine(Path.GetTempPath(), "g101-scoreboard-" + Guid.NewGuid().ToString("N") + ".json");
            sceneOwner = new GameObject("Scoreboard table"); sceneOwner.AddComponent<TableScene>();
            hudOwner = new GameObject("Scoreboard HUD"); hudOwner.SetActive(false);
            panel = UnityEngine.Object.Instantiate(Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings"));
            // Match the HTML's 1280px layout. The batch Editor's 640px Game View otherwise rounds a 1px border
            // up to 2 design pixels, incorrectly turning a 760px row into 758px.
            target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear); target.Create();
            panel.targetTexture = target; panel.scaleMode = PanelScaleMode.ConstantPixelSize; panel.scale = 1;
            hudOwner.AddComponent<UIDocument>().panelSettings = panel;
            hud = hudOwner.AddComponent<TableHud>(); hudOwner.SetActive(true); hud.Initialize(path); hud.Settings.Set("lang", "tr");
            yield return null; yield return null;
        }

        private static void Submit(VisualElement root, string name)
        {
            var element = root.Q<VisualElement>(name); Assert.That(element, Is.Not.Null, name);
            using (var evt = NavigationSubmitEvent.GetPooled()) { evt.target = element; element.SendEvent(evt); }
        }

        [UnityTest]
        public IEnumerator EmptyHistoryHasFourPlayersAndNoInventedScores()
        {
            hud.Render(new TableHud.State { MatchHands = 5 });
            Assert.That(hud.Canvas.Q<VisualElement>("table.openSb"), Is.Not.Null);
            hud.Press("scoreboard"); yield return null; yield return null;
            Assert.That(hud.Window, Is.EqualTo("scoreboard"));
            Assert.That(hud.Canvas.Q<Label>("table.sb.prog").text, Is.EqualTo("0 / 5 el"));
            Assert.That(hud.Canvas.Q<Label>("table.t.sbEmpty").text, Is.EqualTo("Henüz el oynanmadı"));
            Assert.That(hud.Canvas.Query<VisualElement>().ToList().Count(e => e.name.StartsWith("score-matrix-row-")), Is.EqualTo(4));
            Assert.That(hud.Canvas.Query<Label>().ToList().Count(e => e.name.StartsWith("score-cell-") && e.text == "·"), Is.EqualTo(20));
            Assert.That(hud.Canvas.Q<VisualElement>("table.sb.body").layout.height, Is.EqualTo(300).Within(1));
            Submit(hud.Canvas, "table.closeSb"); yield return null;
            Assert.That(hud.Window, Is.Null);
        }

        [UnityTest]
        public IEnumerator HandTabsAndMatrixUseTheSameAuthoritativeHistory()
        {
            var first = new TableHud.ScoreHand { Number = 1, Finisher = 0 };
            var second = new TableHud.ScoreHand { Number = 2, Finisher = 1 };
            int[] a = { -101, 202, 0, 66 }, b = { 33, -202, 202, 0 };
            for (int i = 0; i < 4; i++) { first.Points[i] = first.Totals[i] = a[i]; second.Points[i] = b[i]; second.Totals[i] = a[i] + b[i]; first.Opened[i] = i != 1; }
            hud.Render(new TableHud.State { ScoreHistory = new[] { first, second } });
            hud.OpenScoreboard(); yield return null; yield return null;
            Assert.That(hud.Canvas.Q<Label>("score-cell-1-1").text, Is.EqualTo("-202"));
            Assert.That(hud.Canvas.Q<Label>("table.sb.prog").text, Is.EqualTo("2 / 5 el"));
            Submit(hud.Canvas, "table.sb.tab[0]"); yield return null; yield return null;
            Assert.That(hud.Canvas.Q<Label>("table.r.hand[0]").text, Is.EqualTo("-101"));
            Assert.That(hud.Canvas.Q<Label>("table.r.total[3]").text, Is.EqualTo("202"));
            Assert.That(hud.Canvas.Q<Label>("table.r.note[3]").text, Is.EqualTo("açmadı"));
            Submit(hud.Canvas, "table.sb.tab[1]"); yield return null; yield return null;
            Assert.That(hud.Canvas.Q<Label>("table.r.hand[0]").text, Is.EqualTo("-202"));
            Assert.That(hud.Canvas.Q<Label>("table.r.total[0]").text, Is.EqualTo("0"));
            Assert.That(hud.Canvas.Q<Label>("table.r.note[0]").text, Is.EqualTo("bitirdi"));
            hud.Settings.Set("lang", "en"); yield return null; yield return null;
            Assert.That(hud.Canvas.Q<Label>("table.t.sbTitle").text, Is.EqualTo("SCOREBOARD"));
            Assert.That(hud.Canvas.Q<Label>("table.r.note[0]").text, Is.EqualTo("finished"));
            Assert.That(hud.Current.ScoreHistory.Count, Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator ResultUsesTheSharedTabsWithoutDroppingThePenalty()
        {
            var result = new TableHud.ResultInfo { Title = "1. EL BİTTİ", Sub = "", Head = "Kazandın!", StartedAt = Time.realtimeSinceStartup - 2 };
            result.Rows.Add(new TableHud.ResultRow { Seat = 1, Hand = -101, Total = -101, Note = "bitirdi" });
            result.Rows.Add(new TableHud.ResultRow { Seat = 0, Hand = 123, Penalty = 101, Total = 426, Note = "açtı" });
            hud.Render(new TableHud.State { Phase = "fin", Result = result }); yield return null; yield return null;
            Assert.That(hud.Canvas.Q<VisualElement>("table-result-card").layout.width, Is.EqualTo(822).Within(1));
            Assert.That(hud.Canvas.Q<VisualElement>("table.sb.tab[0]"), Is.Not.Null);
            Assert.That(hud.Canvas.Q<Label>("table.r.hand[1]").text, Is.EqualTo("+224"));
            Assert.That(hud.Canvas.Q<Label>("table.r.total[1]").text, Is.EqualTo("426"));
            Assert.That(hud.Canvas.Q<Label>("table.r.name[1]").text, Is.EqualTo(hud.SeatName(0)));
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator MatrixTracksMatchTheSourceForFiveTenAndFiftyHandsWithoutScrolling()
        {
            foreach (int hands in new[] { 5, 10, 50 })
            {
                hud.Render(new TableHud.State { MatchHands = hands }); hud.OpenScoreboard(); yield return null; yield return null;
                var body = hud.Canvas.Q<VisualElement>("table.sb.body"); var row = body.Q<VisualElement>("score-matrix-row-0");
                var card = hud.Canvas.Q<VisualElement>("table-scoreboard-card");
                TestContext.WriteLine("SOURCE_SCORE_GRID hands=" + hands + " screen=" + Screen.width + "x" + Screen.height + " pixelsPerPoint=" + card.scaledPixelsPerPoint
                    + " card=" + card.layout + " content=" + card.contentRect + " border=" + card.resolvedStyle.borderLeftWidth + "/" + card.resolvedStyle.borderRightWidth
                    + " body=" + body.layout + " rows=" + row.parent.layout + " row=" + row.layout);
                Assert.That(body.Q<ScrollView>(), Is.Null, "the source clips overflow instead of adding a scroll viewport");
                Assert.That(row.layout.width, Is.EqualTo(760).Within(1), "HN " + hands);
                float cellWidth = hands == 5 ? 56 : hands == 10 ? 53.2f : 42;
                Assert.That(body.Q<Label>("score-cell-0-0").layout.width, Is.EqualTo(cellWidth).Within(1), "first numeric track HN " + hands);
                Assert.That(body.Q<Label>("score-cell-0-" + (hands - 1)).layout.width, Is.EqualTo(cellWidth).Within(1), "last numeric track HN " + hands);
                Assert.That(row[1].layout.width, Is.EqualTo(hands == 5 ? 292 : 0).Within(1), "minmax(0,1fr) player track HN " + hands);
                if (hands == 50) Assert.That(row[row.childCount - 1].worldBound.xMin, Is.GreaterThan(body.worldBound.xMax), "42px minimum tracks overflow the source's hidden body");
            }
        }

        [UnityTest]
        public IEnumerator SameCountHistoryCorrectionsRefreshScoresFinisherAndOpeningNotes()
        {
            var hand = new TableHud.ScoreHand { Number = 1, Finisher = 0 };
            int[] points = { -101, 202, 0, 66 };
            for (int k = 0; k < 4; k++) { hand.Points[k] = hand.Totals[k] = points[k]; hand.Opened[k] = false; }
            var state = new TableHud.State { ScoreHistory = new[] { hand } };
            hud.Render(state); hud.OpenScoreboard(); yield return null; yield return null;
            hand.Points[0] = hand.Totals[0] = 33; hand.Points[1] = hand.Totals[1] = -202; hand.Finisher = 1; hand.Opened[2] = true;
            hud.Render(state); yield return null; yield return null;
            Assert.That(hud.Canvas.Q<Label>("score-cell-0-0").text, Is.EqualTo("+33"));
            Assert.That(hud.Canvas.Q<Label>("score-cell-1-0").text, Is.EqualTo("-202"));
            Assert.That(hud.Canvas.Q<Label>("score-cell-0-0").Q<VisualElement>("score-finisher-ring"), Is.Null);
            Assert.That(hud.Canvas.Q<Label>("score-cell-1-0").Q<VisualElement>("score-finisher-ring"), Is.Not.Null);
            Submit(hud.Canvas, "table.sb.tab[0]"); yield return null; yield return null;
            Assert.That(hud.Canvas.Q<Label>("table.r.note[1]").text, Is.EqualTo("açtı"));
            hand.Opened[2] = false; hud.Render(state); yield return null; yield return null;
            Assert.That(hud.Canvas.Q<Label>("table.r.note[1]").text, Is.EqualTo("açmadı"), "same array instance with an opened-only correction must invalidate the recorded copy");
            var card = hud.Canvas.Q<VisualElement>("table-scoreboard-card"); hud.Render(state);
            Assert.That(hud.Canvas.Q<VisualElement>("table-scoreboard-card"), Is.SameAs(card), "identical history must keep its existing view");
        }

        [UnityTest]
        public IEnumerator HandRowsEnterFromTheRightAndCorrectionsKeepTheAnimationClock()
        {
            var hand = new TableHud.ScoreHand { Number = 1, Finisher = 0 };
            var state = new TableHud.State { ScoreHistory = new[] { hand } };
            hud.Render(state); hud.OpenScoreboard(); Submit(hud.Canvas, "table.sb.tab[0]");
            float sampleAt = Time.realtimeSinceStartup + .1f;
            void Sample()
            {
                var motions = (List<Action<float>>)typeof(TableHud).GetField("windowMotions", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(hud);
                foreach (var motion in motions) motion(sampleAt);
            }
            Sample(); var row = hud.Canvas.Q<VisualElement>("result-row-0"); float originalX = row.style.translate.value.x.value;
            Assert.That(originalX, Is.GreaterThan(0), "source drawerIn starts at translateX(+44px)");
            hand.Opened[1] = true; hud.Render(state); Sample();
            Assert.That(hud.Canvas.Q<VisualElement>("result-row-0").style.translate.value.x.value, Is.EqualTo(originalX).Within(.01f), "a content correction must not restart the mounted rows' entrance");
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (hudOwner != null) UnityEngine.Object.Destroy(hudOwner);
            if (sceneOwner != null) UnityEngine.Object.Destroy(sceneOwner);
            if (panel != null) UnityEngine.Object.Destroy(panel);
            yield return null;
            if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
