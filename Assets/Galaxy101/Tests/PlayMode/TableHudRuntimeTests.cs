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
        public IEnumerator ActionPanelKeepsTheSourceVerticalLayoutAndFixedCommandGrid()
        {
            var hud = CreateActionHud(out var table);
            table.CameraRig.enabled = false;
            PlaceRackRight(table, 919.75f);
            hud.Render(new TableHud.State { Phase = "opp", Active = 1 });
            for (int i = 0; i < 10; i++) yield return null;
            var panel = hud.Canvas.Q<VisualElement>("table-actions");
            var meter = hud.Canvas.Q<VisualElement>("table-open-meter");
            var draw = hud.Canvas.Q<VisualElement>("table.doDraw");
            Assert.That(draw, Is.Not.Null, "The source keeps the draw action in its own row, including opponent turns.");
            Assert.That(panel.layout.width, Is.EqualTo(288).Within(1));
            AssertActionPanelInset(hud, panel, 16);
            Assert.That(draw.layout.height, Is.EqualTo(46).Within(1));
            Assert.That(draw.layout.y, Is.GreaterThanOrEqualTo(meter.layout.yMax + 7));
            Assert.That(hud.Canvas.Q<VisualElement>("table-open-gauge").layout.width, Is.EqualTo(46).Within(1));
            var open = hud.Canvas.Q<VisualElement>("table.doOpen");
            var pair = hud.Canvas.Q<VisualElement>("table.doPair");
            var undo = hud.Canvas.Q<VisualElement>("table.doUndo");
            var back = hud.Canvas.Q<VisualElement>("table.doReturn");
            Rect InPanel(VisualElement element)
            {
                var min = panel.WorldToLocal(element.worldBound.min);
                var max = panel.WorldToLocal(element.worldBound.max);
                return new Rect(min, max - min);
            }
            var openBox = InPanel(open); var pairBox = InPanel(pair); var undoBox = InPanel(undo); var backBox = InPanel(back);
            Assert.That(pairBox.y, Is.EqualTo(openBox.y).Within(1));
            Assert.That(pairBox.x - openBox.xMax, Is.EqualTo(6).Within(1));
            Assert.That(undoBox.x, Is.EqualTo(openBox.x).Within(1));
            Assert.That(backBox.x, Is.EqualTo(pairBox.x).Within(1));
            Assert.That(undoBox.y - openBox.yMax, Is.EqualTo(6).Within(1));
            Assert.That(backBox.y, Is.EqualTo(undoBox.y).Within(1));
            Assert.That(backBox.y - pairBox.yMax, Is.EqualTo(6).Within(1));
            foreach (var item in new[] { open, pair, undo, back })
                Assert.That(InPanel(item).height, Is.EqualTo(40).Within(1));
        }

        [UnityTest]
        public IEnumerator ActionPanelDisabledCommandsStayVisibleAndCannotDispatch()
        {
            var hud = CreateActionHud(out _);
            var commands = new System.Collections.Generic.List<string>();
            hud.Command += commands.Add;
            hud.Render(new TableHud.State { Phase = "opp", Active = 1 });
            for (int i = 0; i < 10; i++) yield return null;
            string[] names = { "table.doDraw", "table.doOpen", "table.doPair", "table.doUndo", "table.doReturn" };
            foreach (string name in names)
            {
                var action = hud.Canvas.Q<VisualElement>(name);
                Assert.That(action, Is.Not.Null, name + " keeps its fixed position while disabled.");
                Assert.That(action.resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), name);
                Assert.That(action.resolvedStyle.opacity, Is.EqualTo(.75f).Within(.01f), name);
                SendClick(action);
            }
            Assert.That(commands, Is.Empty, "Disabled source actions must not issue game commands.");
            hud.Render(new TableHud.State { Phase = "discard", CanOpen = true, CanPair = true, CanUndo = true, ShowReturn = true });
            yield return null; yield return null;
            foreach (string name in new[] { "table.doOpen", "table.doPair", "table.doUndo", "table.doReturn" }) SendClick(hud.Canvas.Q<VisualElement>(name));
            Assert.That(commands, Is.EqualTo(new[] { "open", "pairs", "undo", "putback" }));
        }

        [UnityTest]
        public IEnumerator ActionPanelKeepsDisabledControlsAndPaddingInTheUiHitSurface()
        {
            var hud = CreateActionHud(out var table);
            table.CameraRig.enabled = false; PlaceRackRight(table, 919.75f);
            hud.Render(new TableHud.State { Phase = "opp", Active = 1 });
            for (int i = 0; i < 10; i++) yield return null;
            var panel = hud.Canvas.Q<VisualElement>("table-actions");
            foreach (string name in new[] { "table.doDraw", "table.doOpen", "table.doPair", "table.doUndo", "table.doReturn" })
            {
                var action = hud.Canvas.Q<VisualElement>(name);
                Assert.That(action.enabledInHierarchy, Is.False, name);
                var picked = hud.Canvas.panel.Pick(action.worldBound.center);
                Assert.That(picked != null && (picked == action || action.Contains(picked)), Is.True,
                    name + " must remain a UI hit while disabled so RackInput cannot act on the table beneath it.");
            }
            var padding = panel.LocalToWorld(new Vector2(5, panel.layout.height * .5f));
            Assert.That(hud.Canvas.panel.Pick(padding), Is.SameAs(panel), "The panel's padding also blocks the 3D table.");
        }

        [UnityTest]
        public IEnumerator ActionPanelUsesTheCurrentGaugeAndOpenedHandCommands()
        {
            var hud = CreateActionHud(out _);
            var commands = new System.Collections.Generic.List<string>(); hud.Command += commands.Add;
            hud.Render(new TableHud.State { Phase = "draw", CanDraw = true, PairGauge = true, Pairs = 4, PairsBar = 5 });
            yield return null; yield return null;
            Assert.That(hud.Canvas.Q<Label>("table.openSum").text, Is.EqualTo("4"), "Pair sorting switches the gauge from series points to the pair count.");
            Assert.That(hud.Canvas.Q<Label>("table.gaugeSub").text, Is.EqualTo("/ 5 ÇİFT"));
            SendClick(hud.Canvas.Q<VisualElement>("table.doDraw"));
            Assert.That(commands, Is.EqualTo(new[] { "draw" }));

            var opened = new TableHud.State { Phase = "discard", LeftPoints = 23, CanPair = true, CanLayPair = true };
            opened.Opened[0] = 5; opened.OpenedPairs[0] = true;
            hud.Render(opened); yield return null; yield return null;
            Assert.That(hud.Canvas.Q<Label>("table.openSum").text, Is.EqualTo("46"), "An opened pair hand shows its doubled remaining points.");
            Assert.That(hud.Canvas.Q<Label>("table.gaugeSub").text, Is.EqualTo("ELDE"));
            Assert.That(hud.Canvas.Q<Label>("table.leftInfo").text, Is.EqualTo("ELDE KALAN · 23 × 2 = 46"));
            Assert.That(hud.Canvas.Q<VisualElement>("table.doOpen").tooltip, Is.EqualTo("Istakadaki perleri yere aç ve masadaki perlere uyan taşları işle"));
            SendClick(hud.Canvas.Q<VisualElement>("table.doOpen"));
            SendClick(hud.Canvas.Q<VisualElement>("table.doPair"));
            Assert.That(commands, Is.EqualTo(new[] { "draw", "isle" }), "The opened primary lays/adds pairs; the source disables its secondary for pair openers.");
            hud.Settings.Set("lang", "en"); yield return null;
            Assert.That(hud.Canvas.Q<VisualElement>("table.doOpen").tooltip, Is.EqualTo("Lay the sets on your rack and add matching tiles to the sets on the table"));
        }

        [UnityTest]
        public IEnumerator ActionPanelFinishPickerPersistsEachChoiceAndShowsItsName()
        {
            var hud = CreateActionHud(out _);
            hud.Render(new TableHud.State { Phase = "discard", CanFromHand = true });
            for (int i = 0; i < 10; i++) yield return null;
            string[] keys = { "nuke", "blackhole", "supernova", "meteor" };
            string[] names = { "Nükleer", "Kara delik", "Süpernova", "Meteor" };
            Assert.That(hud.Canvas.Q<VisualElement>("table.bombCard"), Is.Not.Null);
            for (int i = 0; i < keys.Length; i++)
            {
                var choice = hud.Canvas.Q<VisualElement>("table.bombFx[" + i + "]");
                Assert.That(choice, Is.Not.Null);
                Assert.That(choice.layout.width, Is.EqualTo(30).Within(1));
                SendClick(choice);
                yield return null; yield return null;
                Assert.That(hud.Settings.Text("bombFx"), Is.EqualTo(keys[i]));
                Assert.That(hud.Canvas.Q<Label>("table.bombFxName").text, Is.EqualTo(names[i]));
                Assert.That(hud.Canvas.Q<VisualElement>("table.bombFx[" + i + "]").ClassListContains("selected"), Is.True);
            }
            hud.Render(new TableHud.State { Phase = "discard", CanFromHand = false });
            yield return null;
            Assert.That(hud.Canvas.Q<VisualElement>("table.bombCard"), Is.Null, "The source shows the finish card only when the hand can finish.");
        }

        [UnityTest]
        public IEnumerator ActionPanelNarrowsThenScalesAsTheProjectedRackMoves()
        {
            var hud = CreateActionHud(out var table);
            table.CameraRig.enabled = false;
            PlaceRackRight(table, 919.75f);
            hud.Render(new TableHud.State { Phase = "draw" });
            for (int i = 0; i < 10; i++) yield return null;
            var panel = hud.Canvas.Q<VisualElement>("table-actions");
            Assert.That(panel.layout.width, Is.EqualTo(288).Within(1));
            Assert.That(panel.style.scale.value.value.x, Is.EqualTo(1).Within(.001f));
            PlaceRackRight(table, 1039.75f);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(panel.layout.width, Is.EqualTo(220).Within(1), "fitPanel first preserves text size by narrowing to the rack's free space.");
            AssertActionPanelInset(hud, panel, 12);
            Assert.That(panel.style.scale.value.value.x, Is.EqualTo(1).Within(.001f));
            PlaceRackRight(table, 1099.75f);
            yield return new WaitForSecondsRealtime(.65f);
            Assert.That(panel.layout.width, Is.EqualTo(196).Within(1), "The source's minimum unscaled width is 196px.");
            Assert.That(panel.style.scale.value.value.x, Is.EqualTo(.86f).Within(.001f), "Scale rounds to 0.02 increments and stops at 86 percent.");
            PlaceRackRight(table, 919.75f);
            yield return new WaitForSecondsRealtime(.65f);
            Assert.That(panel.layout.width, Is.EqualTo(288).Within(1), "Camera/rack movement restores full width without rebuilding the HUD.");
            AssertActionPanelInset(hud, panel, 16);
            Assert.That(panel.style.scale.value.value.x, Is.EqualTo(1).Within(.001f));
        }

        [UnityTest]
        public IEnumerator DealSkipMatchesTheSourceButtonAndDispatchesOnlyItsOwnCommand()
        {
            var hud = CreateActionHud(out _);
            var commands = new System.Collections.Generic.List<string>(); hud.Command += commands.Add;
            hud.Render(new TableHud.State { Phase = "deal", Deal = new TableHud.DealInfoCard { Dealer = 0, Step = "roll", CanSkip = true } });
            yield return null; yield return null;
            var skip = hud.Canvas.Q<VisualElement>("table.dh.skip");
            Assert.That(skip, Is.Not.Null, "The current source adds Geç beside the dice in the deal card.");
            float until = Time.realtimeSinceStartup + 2;
            while (skip.Q<Label>().style.width.value.value <= 0 && Time.realtimeSinceStartup < until) yield return null;
            yield return null;
            Assert.That(skip.layout.height, Is.EqualTo(40).Within(1));
            Assert.That(skip.layout.width, Is.EqualTo(77.21875f).Within(1), "Source TR button: 12/14 padding, 16px icon, 7px gap, 14px Montserrat-800.");
            Assert.That(skip.Q<Label>().text, Is.EqualTo("Geç"));
            Assert.That(skip.tooltip, Is.EqualTo("Zar atma ve dağıtmayı geç, oyuna hemen başla"));
            using (var submit = NavigationSubmitEvent.GetPooled()) { submit.target = skip; skip.SendEvent(submit); }
            Assert.That(commands, Is.EqualTo(new[] { "skipDeal" }));
            hud.Settings.Set("lang", "en"); yield return null; yield return null;
            Assert.That(hud.Canvas.Q<VisualElement>("table.dh.skip").Q<Label>().text, Is.EqualTo("Skip"));
        }

        [UnityTest]
        public IEnumerator DealSkipVoteShowsTheSourceWaitingStateAndDisappearsWhenUnavailable()
        {
            var hud = CreateActionHud(out _);
            var commands = new System.Collections.Generic.List<string>(); hud.Command += commands.Add;
            hud.Render(new TableHud.State { Phase = "deal", Deal = new TableHud.DealInfoCard { Step = "deal", CanSkip = true, SkipWait = true } });
            yield return null; yield return null;
            Assert.That(hud.Canvas.Q<VisualElement>("table.dh.skip"), Is.Null);
            var waiting = hud.Canvas.Q<VisualElement>("table.dh.skipWait"); Assert.That(waiting, Is.Not.Null);
            Assert.That(waiting.layout.height, Is.EqualTo(40).Within(1));
            Assert.That(waiting.Q<Label>().text, Is.EqualTo("Bekleniyor"));
            var spinner = waiting.Q<VisualElement>("table.dh.skipSpinner");
            Assert.That(spinner.layout.width, Is.EqualTo(16).Within(1), "Source content width12 + two2px borders.");
            Assert.That(spinner.layout.height, Is.EqualTo(16).Within(1));
            SendClick(waiting); Assert.That(commands, Is.Empty, "A waiting vote cannot be submitted twice.");
            hud.Render(new TableHud.State { Phase = "deal", Deal = new TableHud.DealInfoCard { Step = "rack", CanSkip = false } });
            yield return null;
            Assert.That(hud.Canvas.Q<VisualElement>("table.dh.skip"), Is.Null);
            Assert.That(hud.Canvas.Q<VisualElement>("table.dh.skipWait"), Is.Null);
        }

        [UnityTest]
        public IEnumerator DealSkipHoverKeepsItsFilterListBounded()
        {
            var hud = CreateActionHud(out _);
            hud.Render(new TableHud.State { Phase = "deal", Deal = new TableHud.DealInfoCard { Step = "roll", CanSkip = true } });
            yield return null; yield return null;
            var button = hud.Canvas.Q<VisualElement>("table.dh.skip");
            int count = button.style.filter.value.Count;
            for (int i = 0; i < 3; i++)
            {
                using (var enter = PointerEnterEvent.GetPooled()) { enter.target = button; button.SendEvent(enter); }
                yield return new WaitForSecondsRealtime(.18f);
                using (var leave = PointerLeaveEvent.GetPooled()) { leave.target = button; button.SendEvent(leave); }
                yield return new WaitForSecondsRealtime(.18f);
                Assert.That(button.style.filter.value.Count, Is.EqualTo(count), "Repeated hover must not accumulate transparent drop-shadow filters.");
            }
        }

        private TableHud CreateActionHud(out TableScene table)
        {
            statePath = Path.Combine(Path.GetTempPath(), "galaxy-actions-" + Guid.NewGuid().ToString("N") + ".json");
            sceneOwner = new GameObject("GalaxyTable"); table = sceneOwner.AddComponent<TableScene>();
            hudOwner = new GameObject("TableHud"); hudOwner.SetActive(false);
            hudOwner.AddComponent<UIDocument>().panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            var hud = hudOwner.AddComponent<TableHud>(); hudOwner.SetActive(true); hud.Initialize(statePath);
            hud.Settings.Set("lang", "tr"); hud.Settings.Set("view", "seat");
            return hud;
        }

        private static void SendClick(VisualElement target)
        {
            Assert.That(target, Is.Not.Null);
            using (var click = ClickEvent.GetPooled()) { click.target = target; target.SendEvent(click); }
        }

        private static void AssertActionPanelInset(TableHud hud, VisualElement panel, float right)
        {
            var edge = hud.Canvas.WorldToLocal(panel.worldBound.max);
            float physicalRight = hud.Canvas.contentRect.xMax - edge.x;
            float physicalBottom = hud.Canvas.contentRect.yMax - edge.y;
            string evidence = "inlineRight=" + panel.style.right.value.value + ", resolvedRight=" + panel.resolvedStyle.right
                + ", layout=" + panel.layout + ", canvasWorld=" + hud.Canvas.worldBound + ", panelWorld=" + panel.worldBound
                + ", physicalRight=" + physicalRight + ", physicalBottom=" + physicalBottom;
            Debug.Log("GALAXY101_ACTION_PANEL_INSET " + evidence);
            Assert.That(panel.style.right.value.value, Is.EqualTo(right).Within(.01f), evidence);
            Assert.That(physicalRight, Is.EqualTo(right).Within(1), evidence);
            Assert.That(physicalBottom, Is.EqualTo(14).Within(1), evidence);
        }

        private static void PlaceRackRight(TableScene table, float targetX)
        {
            var camera = table.CameraRig.Camera;
            Vector3 rightmost = default; float max = float.NegativeInfinity;
            foreach (float x in new[] { -.46f, .46f })
                foreach (float y in new[] { -.01f, .2f })
                    foreach (float z in new[] { -.04f, .04f })
                    {
                        var world = table.RackPose(new Vector3(x, y, z)).position;
                        float projected = camera.WorldToViewportPoint(world).x;
                        if (projected > max) { max = projected; rightmost = world; }
                    }
            var point = camera.WorldToViewportPoint(rightmost);
            var target = camera.ViewportToWorldPoint(new Vector3(targetX / 1280, point.y, point.z));
            table.RackRoot(0).position += target - rightmost;
        }

        [UnityTest]
        public IEnumerator ChatUsesTheServerLimitAndWrapsLongMessages()
        {
            statePath = Path.Combine(Path.GetTempPath(), "galaxy-chat-" + Guid.NewGuid().ToString("N") + ".json");
            sceneOwner = new GameObject("GalaxyTable"); sceneOwner.AddComponent<TableScene>();
            hudOwner = new GameObject("TableHud"); hudOwner.SetActive(false);
            hudOwner.AddComponent<UIDocument>().panelSettings = Resources.Load<PanelSettings>("Galaxy101/UI/PanelSettings");
            var hud = hudOwner.AddComponent<TableHud>(); hudOwner.SetActive(true); hud.Initialize(statePath);
            string sent = null;
            hud.SendGiftOnline = (gift, seat) => System.Threading.Tasks.Task.FromResult<string>(null);
            hud.SendChatOnline = (text, quick) => { sent = text; return System.Threading.Tasks.Task.FromResult<string>(null); };
            string message = new string('x', 120);
            hud.SendChat(message + "!");
            Assert.That(sent, Is.EqualTo(message), "D-32: preserve all 120 permitted characters and truncate excess");
            hud.OpenChat(); yield return null;
            var input = hud.Canvas.Q<TextField>();
            input.value = message;
            Assert.That(input.value, Is.EqualTo(message), "The input accepts the same limit as the server");
            hud.ChatFrom(1, message, null);
            for (int i = 0; i < 10; i++) yield return null;
            var bubble = hud.Canvas.Q<VisualElement>("table.m.text[0]");
            Assert.That(bubble.worldBound.width, Is.LessThanOrEqualTo(240.01f), "Scaled world bounds can differ by a floating-point rounding fraction of a pixel.");
            Assert.That(bubble.worldBound.height, Is.GreaterThan(14 * 1.35f + 14), "Long messages wrap inside the bubble");
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
            Assert.That(canvas.Q<Label>("table.t.chipsCap")?.text, Is.EqualTo("EL"), "chipsCap: EL over the hand count before the last hand");
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
            Assert.That(canvas.Q<VisualElement>("anchor-ind"), Is.Null, "G101-Masa draws no GÖSTERGE tag in 3D");

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

            // UF-520: one compact message slot under the top bar; a flash takes it over from the turn band, which returns after.
            Assert.That(canvas.Q<VisualElement>("table-toast-row").worldBound.yMin, Is.GreaterThan(canvas.Q<VisualElement>("table-top-bar").worldBound.yMax - 1), "the band never covers the top bar");
            hud.Post(TableHud.ToastKind.Penalty, "Eksik açılış · 88 / 101", amount: 101);
            yield return new WaitForSecondsRealtime(.5f);
            Assert.That(canvas.Q<VisualElement>("table-message-row").resolvedStyle.top, Is.EqualTo(73f));
            Assert.That(canvas.Q<VisualElement>("table-toast-row").resolvedStyle.display, Is.EqualTo(DisplayStyle.None));
            Assert.That(hud.ActiveToast, Does.StartWith("Eksik açılış · 88 / 101 · +101"));
            var flash = canvas.Q<VisualElement>("table-message");
            Assert.That(flash[1].layout.x - flash[0].layout.xMax, Is.EqualTo(7).Within(1), "The independently rebuilt flash keeps its compact 7px icon gap");
            Assert.That(flash.worldBound.height, Is.LessThan(32f), "compact pill");

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
