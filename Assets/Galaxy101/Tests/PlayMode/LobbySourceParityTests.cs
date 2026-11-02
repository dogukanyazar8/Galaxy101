using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Galaxy101.Contracts;
using Galaxy101.Diagnostics;
using Galaxy101.Online;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Galaxy101.Tests
{
    public sealed class LobbySourceParityTests
    {
        private GameObject owner;
        private PanelSettings panel;
        private RenderTexture target;
        private MenuView view;
        private LocalizationBridge localization;
        private LobbyScreen lobby;
        private G101DemoLobby backend;
        private string statePath;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            statePath = Path.Combine(Path.GetTempPath(), "g101-lobby-parity-" + Guid.NewGuid().ToString("N") + ".json");
            owner = new GameObject("Source lobby parity"); owner.SetActive(false);
            panel = UnityEngine.Object.Instantiate(Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings"));
            SetDensity(1);
            var document = owner.AddComponent<UIDocument>(); document.panelSettings = panel; owner.SetActive(true);
            document.rootVisualElement.style.width = 1280; document.rootVisualElement.style.height = 720;
            var model = MenuModel.LoadResources(statePath); model.Settings["lang"] = "tr";
            localization = new LocalizationBridge(model); view = new MenuView(document.rootVisualElement, model, new MenuServices(model), localization);
            backend = new G101DemoLobby(); lobby = new LobbyScreen(view, backend); lobby.Show();
            yield return null; yield return null;
        }

        private void SetDensity(float density)
        {
            var previous = target;
            target = new RenderTexture(Mathf.RoundToInt(1280 * density), Mathf.RoundToInt(720 * density), 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear); target.Create();
            panel.targetTexture = target; panel.scaleMode = PanelScaleMode.ConstantPixelSize; panel.scale = density;
            if (previous != null) { previous.Release(); UnityEngine.Object.Destroy(previous); }
        }

        private static List<Label> Labels(VisualElement root) => root.Query<Label>().ToList();

        [UnityTest]
        public IEnumerator SourceDefaultOpenerBadgesIncludeTablesWithOnlyPlayablePenaltyDisabled()
        {
            Assert.That(backend.Tables.Any(t => !t.Penalties), Is.True, "source t3 has isle=false while opener is absent/default-on");
            Assert.That(Labels(lobby.Layer).Count(l => l.text == "Açtıran ×10"), Is.EqualTo(backend.Tables.Count));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CreateAndWaitingSummariesIncludeTheOpeningTilePenaltyInSourceOrder()
        {
            lobby.OpenCreate(); yield return null; yield return null;
            var summary = lobby.Layer.Q<VisualElement>("create.summary.rules");
            Assert.That(summary.Q<Label>("lobby.q.k[3]").text, Is.EqualTo("Açtıran taş cezası"));
            Assert.That(summary.Q<Label>("lobby.q.v[3]").text, Is.EqualTo("×10"));
            var close = lobby.Layer.Q<VisualElement>("lobby.closeCreate#2");
            using (var click = ClickEvent.GetPooled()) { click.target = close; close.SendEvent(click); }
            backend.EnterWait(backend.Pick("open"), true); yield return null; yield return null;
            Assert.That(Labels(lobby.Layer).Any(l => l.text == "Açtıran taş cezası"), Is.True);
            var strings = JObject.Parse(Resources.Load<TextAsset>("Galaxy101/Strings/strings_v4").text)["strings"];
            Assert.That((string)strings["lobby.STR.sOpen"]?["en"], Is.EqualTo("Opening-tile penalty"));
            Assert.That((string)strings["lobby.STR.bOpen"]?["en"], Is.EqualTo("Opener ×10"));
        }

        [UnityTest]
        public IEnumerator ActualV4PenaltyVisibilityFollowsTheExistingAuthoritativeUmbrellaFlag()
        {
            foreach (bool enabled in new[] { false, true })
            {
                var table = LobbyClient.Summary(new V4Table { Id = Guid.NewGuid().ToString(), Rules = new V4TableRules { Penalties = enabled }, Seats = new List<V4Seat> { null, null, null, null } });
                var badges = (IEnumerable<VisualElement>)typeof(LobbyScreen).GetMethod("Badges", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(lobby, new object[] { table, false });
                Assert.That(badges.OfType<Label>().Any(l => l.text == "Açtıran ×10"), Is.EqualTo(enabled));
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CreateFormReservesTheSourceThinScrollbarGutter()
        {
            lobby.OpenCreate(); yield return new WaitForSeconds(.4f);
            var scroll = lobby.Layer.Q<ScrollView>("lobby-create-scroll");
            Assert.That(scroll.verticalScrollerVisibility, Is.EqualTo(ScrollerVisibility.Auto));
            Assert.That(scroll.verticalScroller.layout.width, Is.EqualTo(10).Within(.01f));
            Assert.That(lobby.Layer.Q<VisualElement>("lobby-create-name").worldBound.xMax, Is.EqualTo(808).Within(.51f));
        }

        [UnityTest]
        public IEnumerator CreateNormalFontLinesUseTheActualPanelPixelDensity()
        {
            SetDensity(1.5f); yield return null;
            lobby.OpenCreate(); yield return new WaitForSeconds(.4f);
            var text = lobby.Layer.Q<VisualElement>("lobby-create-type").Query<Label>().ToList().First();
            TestContext.WriteLine("create scaledPixelsPerPoint=" + text.scaledPixelsPerPoint);
            Assert.That(text.scaledPixelsPerPoint, Is.EqualTo(1.5f).Within(.01f));
            Assert.That(text.style.height.value.value, Is.EqualTo(16.666667f).Within(.01f), "the source's 14px Montserrat normal line at DPR 1.5");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            lobby?.Hide(); view?.Dispose(); localization?.Dispose();
            if (owner != null) UnityEngine.Object.Destroy(owner);
            yield return null;
            if (panel != null) { panel.targetTexture = null; UnityEngine.Object.Destroy(panel); }
            if (target != null) { target.Release(); UnityEngine.Object.Destroy(target); }
            if (File.Exists(statePath)) File.Delete(statePath);
        }
    }
}
