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
    /// <summary>
    /// Table screen: galaxy background + transparent 3D view + HUD in one UI framebuffer. Set GALAXY_TABLE_HUD_CAPTURE
    /// to an absolute .png path to keep the 1280×720 frame for comparison with screenshots/table/T05.
    /// </summary>
    public sealed class TableHudRuntimeTests
    {
        private GameObject sceneOwner, hudOwner;
        private PanelSettings capturePanel;
        private RenderTexture framebuffer;
        private string statePath;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (hudOwner != null) UnityEngine.Object.Destroy(hudOwner);
            if (sceneOwner != null) UnityEngine.Object.Destroy(sceneOwner);
            if (capturePanel != null) UnityEngine.Object.Destroy(capturePanel);
            if (framebuffer != null) { framebuffer.Release(); UnityEngine.Object.Destroy(framebuffer); }
            if (statePath != null && File.Exists(statePath)) File.Delete(statePath);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HandResultIncludesPenaltiesAndKeepsHandXpSeparateFromTheMatchTotal()
        {
            statePath = Path.Combine(Path.GetTempPath(), "galaxy-result-" + Guid.NewGuid().ToString("N") + ".json");
            sceneOwner = new GameObject("GalaxyTable"); sceneOwner.AddComponent<TableScene>();
            hudOwner = new GameObject("TableHud"); hudOwner.SetActive(false);
            hudOwner.AddComponent<UIDocument>().panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            var hud = hudOwner.AddComponent<TableHud>(); hudOwner.SetActive(true); hud.Initialize(statePath);
            var result = new TableHud.ResultInfo
            {
                Title = "2. EL BİTTİ", Sub = "BURAK BİTİRDİ", Head = "Burak kazandı", Winner = 1,
                Xp = 60, TotalXp = 400, StartedAt = Time.realtimeSinceStartup,
            };
            result.Rows.Add(new TableHud.ResultRow { Seat = 1, Hand = -101, Total = -202, Note = "bitirdi" });
            result.Rows.Add(new TableHud.ResultRow { Seat = 0, Hand = 123, Penalty = 101, Total = 426, Note = "açtı" });
            hud.Render(new TableHud.State { Phase = "end", Hand = 2, MatchHands = 5, Result = result });
            yield return null; yield return null;
            var canvas = hud.Canvas;
            Assert.That(canvas.Q<Label>("table.r.hand[1]")?.text, Is.EqualTo("+224"), "R-58: this hand includes its +101 penalty");
            Assert.That(canvas.Q<Label>("table.r.total[1]")?.text, Is.EqualTo("426"), "the running score is supplied by the engine");
            Assert.That(canvas.Q<Label>("table.res.xp")?.text, Is.EqualTo("+60 XP"), "the card shows hand XP, not accumulated XP");
            Assert.That(canvas.Q<Label>("table.t.chipsCap")?.text, Is.EqualTo("EL"));
            Assert.That(canvas.Q<Label>("table.res.chips")?.text, Is.EqualTo("2 / 5"));
            Assert.That(canvas.Q<Label>("table.r.name[1]")?.text, Is.EqualTo(hud.SeatName(0)), "the package has no extra '(sen)' suffix");

            result.LastHand = true;
            hud.Render(new TableHud.State { Phase = "end", Hand = 5, MatchHands = 5, Result = result });
            yield return null;
            Assert.That(canvas.Q<Label>("table.nextLabel")?.text, Is.EqualTo("Maçı bitir"));
        }

        [UnityTest]
        public IEnumerator TableHudComposesBackgroundViewAndAnchors()
        {
            statePath = Path.Combine(Path.GetTempPath(), "galaxy-table-" + Guid.NewGuid().ToString("N") + ".json");
            sceneOwner = new GameObject("GalaxyTable"); var table = sceneOwner.AddComponent<TableScene>();
            hudOwner = new GameObject("TableHud"); hudOwner.SetActive(false);
            var document = hudOwner.AddComponent<UIDocument>(); document.panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            var hud = hudOwner.AddComponent<TableHud>(); hudOwner.SetActive(true); hud.Initialize(statePath);

            // Render the owned gamma panel into a fixed 1280×720 target, as the menu acceptance capture does.
            framebuffer = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear) { name = "Table HUD capture" };
            capturePanel = UnityEngine.Object.Instantiate(document.panelSettings); capturePanel.targetTexture = framebuffer; document.panelSettings = capturePanel;
            for (int i = 0; i < 30; i++) yield return null;

            Assert.That(table.CameraRig.Camera.targetTexture, Is.Not.Null, "The 3D camera draws into the HUD's view layer.");
            var canvas = hud.Canvas;
            foreach (var name in new[] { "table-background", "table-3d", "table-vignette", "table-anchors", "table-top-shade", "table-hud" })
                Assert.That(canvas.Q<VisualElement>(name), Is.Not.Null, name);
            Assert.That(canvas.Q<Label>("table-toast").text, Does.StartWith("Sıra sende"));
            Assert.That(canvas.Q<Label>("table.deckCount").text, Is.EqualTo("24"));
            Assert.That(canvas.Q<Label>("table.openSum").text, Is.EqualTo("88"));

            // T05: Ayşe (seat 2) sits above the table centre, Zeynep (seat 1) right, Burak (seat 3) left.
            float X(string key) => canvas.Q<VisualElement>("anchor-" + key).resolvedStyle.left;
            float Y(string key) => canvas.Q<VisualElement>("anchor-" + key).resolvedStyle.top;
            Assert.That(X("a2"), Is.InRange(560f, 720f)); Assert.That(Y("a2"), Is.InRange(150f, 260f));
            Assert.That(X("a1"), Is.GreaterThan(1000f)); Assert.That(X("a3"), Is.LessThan(280f));
            Assert.That(X("ind"), Is.InRange(560f, 720f));

            // A rebuild under a resting pointer keeps the hovered button lit (UI Toolkit re-targets the hover).
            var settings = canvas.Q<VisualElement>("table.openSettings"); var centre = settings.worldBound.center;
            using (var move = PointerMoveEvent.GetPooled(new Event { type = EventType.MouseMove, mousePosition = centre })) canvas.panel.visualTree.SendEvent(move);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.That(canvas.Q<VisualElement>("table.openSettings").style.filter.value, Is.Not.Null.And.Not.Empty, "hovered before the rebuild");
            hud.Render(hud.Current); for (int i = 0; i < 3; i++) yield return null;
            Assert.That(canvas.Q<VisualElement>("table.openSettings"), Is.Not.SameAs(settings), "the HUD was rebuilt");
            Assert.That(canvas.Q<VisualElement>("table.openSettings").style.filter.value, Is.Not.Null.And.Not.Empty, "still hovered after the rebuild");
            using (var away = PointerMoveEvent.GetPooled(new Event { type = EventType.MouseMove, mousePosition = new Vector2(640, 400) })) canvas.panel.visualTree.SendEvent(away);
            for (int i = 0; i < 3; i++) yield return null;

            // G101-Masa flash: one pill at top 132 under the turn band, which stays.
            hud.Post(TableHud.ToastKind.Penalty, "Eksik açılış · 88 / 101", amount: 101);
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(canvas.Q<VisualElement>("table-message-row").resolvedStyle.top, Is.EqualTo(132f));
            Assert.That(canvas.Q<VisualElement>("table-toast-row").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));
            Assert.That(hud.ActiveToast, Does.StartWith("Eksik açılış · 88 / 101 · +101"));

            var previous = RenderTexture.active; RenderTexture.active = framebuffer;
            var pixels = new Texture2D(1280, 720, TextureFormat.RGBA32, false); pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            RenderTexture.active = previous;
            // The window region (top left) shows the galaxy background, not the camera's transparent clear.
            var sky = pixels.GetPixel(300, 720 - 118);
            Assert.That(sky.r + sky.g + sky.b, Is.GreaterThan(0.15f), "Planet/halo must be visible through the room window.");
            string output = Environment.GetEnvironmentVariable("GALAXY_TABLE_HUD_CAPTURE");
            if (!string.IsNullOrEmpty(output)) { File.WriteAllBytes(output, pixels.EncodeToPNG()); Debug.Log("GALAXY101_TABLE_HUD_CAPTURE " + output); }
            UnityEngine.Object.Destroy(pixels);
        }
    }
}
