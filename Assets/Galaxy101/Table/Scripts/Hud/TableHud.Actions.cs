using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101.Table
{
    public sealed partial class TableHud
    {
        // G101-Masa panelRef / fitPanel: width changes before text and controls scale.
        private void FitActionPanel(VisualElement panel)
        {
            if (table?.CameraRig?.Camera == null || IsTwoDimensional) return;
            var camera = table.CameraRig.Camera;
            float right = float.NegativeInfinity;
            for (int ix = 0; ix < 2; ix++)
                for (int iy = 0; iy < 2; iy++)
                    for (int iz = 0; iz < 2; iz++)
                    {
                        var point = table.RackPose(new Vector3(ix == 0 ? -.46f : .46f, iy == 0 ? -.01f : .2f, iz == 0 ? -.04f : .04f)).position;
                        var projected = camera.WorldToViewportPoint(point);
                        if (projected.z <= 0 || float.IsNaN(projected.x) || float.IsInfinity(projected.x)) return;
                        right = Mathf.Max(right, projected.x * Width);
                    }
            float free = Width - 12 - 8 - right, width = 288, scale = 1;
            if (free < 288)
            {
                width = Mathf.Max(196, Mathf.Floor(free));
                if (free < 196) scale = Mathf.Max(.86f, free / 196);
            }
            scale = Mathf.Floor(scale * 50 + .5f) / 50;
            panel.style.width = width;
            panel.style.right = width < 288 ? 12 : 16;
            panel.style.scale = new Scale(Vector2.one * scale);
        }

        private void BuildActionPanel(State s)
        {
            var panel = new VisualElement { name = "table-actions", pickingMode = PickingMode.Position };
            panel.style.position = Position.Absolute; panel.style.right = 16; panel.style.bottom = 14; panel.style.width = 288;
            panel.style.transformOrigin = new TransformOrigin(Length.Percent(100), Length.Percent(100));
            panel.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("scale") };
            panel.style.transitionDuration = new List<TimeValue> { new TimeValue(.25f, TimeUnit.Second) };
            panel.style.transitionTimingFunction = new List<EasingFunction> { new EasingFunction(EasingMode.Ease) };
            Pad(panel, 10, 10, 10, 10);
            view.Decorate(panel, "linear-gradient(180deg,rgba(18,22,52,.84),rgba(6,8,24,.84))", 20);
            Border(panel, 1, new Color(1, 1, 1, .09f)); GalaxyFilters.BackdropBlur(panel, 8);
            GalaxyFilters.Shadow(panel, 0, 14, 16, new Color(0, 0, 0, .4f));

            bool opened = s.Opened[0] > 0, pairOpened = opened && s.OpenedPairs[0];
            var meter = Row(10); meter.name = "table-open-meter"; meter.style.flexShrink = 0;
            var gauge = new VisualElement { name = "table-open-gauge", pickingMode = PickingMode.Ignore };
            gauge.style.width = gauge.style.height = 46; gauge.style.flexShrink = 0;
            bool pairGauge = opened ? pairOpened : s.PairGauge;
            int value = opened ? s.LeftPoints * (pairOpened ? 2 : 1) : pairGauge ? s.Pairs : s.OpenSum;
            int need = pairGauge ? s.PairsBar : s.OpenBar;
            var arc = new ArcRing(view.C(opened ? "#FF8A94" : value >= need ? "#3CCB6E" : "var(--a1)"), new Color(1, 1, 1, .1f))
            { Fraction = opened ? 1 : need > 0 ? Mathf.Min(100, Mathf.Round(value / (float)need * 100)) / 100f : 0 };
            MenuView.Place(arc, 0, 0, 46, 46); gauge.Add(arc);
            var dial = new VisualElement { pickingMode = PickingMode.Ignore }; MenuView.Place(dial, 4, 4, 38, 38);
            dial.style.alignItems = Align.Center; dial.style.justifyContent = Justify.Center; view.Decorate(dial, "var(--dp)", 19);
            var sum = Text(value.ToString(), "Montserrat-700", 15, "#F4F1E8"); sum.name = "table.openSum"; dial.Add(sum);
            var sub = Text(opened ? L("ELDE", "IN HAND") : "/ " + need + (pairGauge ? L(" ÇİFT", " PAIRS") : ""), "Baloo2-800", 7, "var(--mt)");
            sub.name = "table.gaugeSub"; dial.Add(sub); gauge.Add(dial); meter.Add(gauge);

            var information = new VisualElement { pickingMode = PickingMode.Ignore }; information.style.flexGrow = 1; information.style.minWidth = 0;
            if (opened)
            {
                var openedRow = Row(6); openedRow.style.marginBottom = 3;
                var led = new VisualElement { pickingMode = PickingMode.Ignore }; led.style.width = led.style.height = 7; led.style.flexShrink = 0;
                view.Decorate(led, "#3CCB6E", 4); GalaxyFilters.Shadow(led, 0, 0, 4, view.C("#3CCB6E")); openedRow.Add(led);
                var status = Text(pairOpened ? L("AÇILDI · " + s.Opened[0] + " ÇİFT", "OPENED · " + s.Opened[0] + " PAIRS") : L("AÇILDI · SERİ " + s.Opened[0], "OPENED · RUNS " + s.Opened[0]), "Baloo2-800", 9, "#8EF0B0");
                MenuView.Tracking(status, 1, 9); openedRow.Add(status); information.Add(openedRow);
                var left = Text(pairOpened ? L("ELDE KALAN · " + s.LeftPoints + " × 2 = " + value, "IN HAND · " + s.LeftPoints + " × 2 = " + value) : L("ELDE KALAN · " + s.LeftPoints + " PUAN", "IN HAND · " + s.LeftPoints + " PTS"), "Baloo2-800", 9, "#FFB3BA");
                left.name = "table.leftInfo"; MenuView.Tracking(left, 1, 9); left.style.marginBottom = 3; information.Add(left);
            }
            var sorts = Row(3); Pad(sorts, 3, 3, 3, 3); view.Decorate(sorts, "rgba(0,0,0,.28)", 11);
            foreach (bool runs in new[] { true, false })
            {
                var sort = SortButton(runs ? "table.sortRun" : "table.sortPair", runs ? L("Seri diz", "Sort runs") : L("Çift diz", "Sort pairs"));
                Pad(sort, 0, 0, 0, 0); sort.style.height = 26; sort.style.flexBasis = 0; sort.style.flexGrow = 1; sort.style.minWidth = 0;
                view.Decorate(sort, "var(--pnH)", 8, 2, "var(--pnS)"); sorts.Add(Clickable(sort, runs ? "sort-run" : "sort-pair"));
            }
            information.Add(sorts); meter.Add(information); panel.Add(meter);

            var draw = PanelCommand("table.doDraw", L("Taş çek", "Draw"), s.CanDraw, true, 46, 17, "draw", 4);
            draw.tooltip = L("Desteden taş çek · soldaki taşı almak için ona tıkla", "Draw from the deck · click the left tile to take it instead");
            draw.userData = 10f; draw.style.marginTop = 8;
            var tiles = new VisualElement { pickingMode = PickingMode.Ignore }; tiles.style.width = 26; tiles.style.height = 32; tiles.style.flexShrink = 0;
            for (int i = 0; i < 2; i++)
            {
                var tile = new VisualElement { pickingMode = PickingMode.Ignore }; MenuView.Place(tile, i == 0 ? 6 : 1, i == 0 ? 0 : 5, 19, 27);
                view.Decorate(tile, "linear-gradient(var(--tf1),var(--tf2))", 5, 3, "var(--te)"); tiles.Add(tile);
            }
            draw.Insert(0, tiles);
            if (s.CanDraw) PulsePanelCommand(draw, 1.8f, view.C("var(--gl)"));
            var count = Text(s.DeckCount.ToString(), "Baloo2-800", 12, s.CanDraw ? "var(--aT)" : "var(--mt)"); count.name = "table.doDraw.n";
            count.style.height = 22; count.style.unityTextAlign = TextAnchor.MiddleCenter; Pad(count, 0, 8, 0, 8); view.Decorate(count, "rgba(0,0,0,.14)", 11); draw.Add(count); panel.Add(draw);

            var grid = new VisualElement { name = "table-action-grid", pickingMode = PickingMode.Ignore }; grid.style.marginTop = 8;
            var first = Row(6); var second = Row(6); second.style.marginTop = 6;
            bool canOpen = opened ? s.CanOpen || s.CanIsle || pairOpened && s.CanPair : s.CanOpen;
            var open = PanelCommand("table.doOpen", opened ? TableStrings.Get("isle", Lang) : L("Aç · ", "Open · ") + s.OpenPotential + "/" + s.OpenBar, canOpen, opened, 40, 13, opened ? "isle" : "open");
            if (!opened && s.ShortOpen && canOpen) WarnPanelCommand(open);
            open.tooltip = !opened && s.ShortOpen ? L("Sayı yetmiyor · açarsan hatalı açma cezası alırsın", "Not enough points · opening now costs a wrong-opening penalty") : opened ? TableStrings.Get("isleTip", Lang) : "";
            bool canPair = opened ? !pairOpened && s.CanLayPair : s.CanPair;
            var pairs = PanelCommand("table.doPair", opened ? TableStrings.Get("layPair", Lang) : L("Çift aç · ", "Open pairs · ") + s.PairsPotential + "/" + s.PairsBar, canPair, false, 40, 13, "pairs");
            if (!opened && s.ShortPairs && canPair) WarnPanelCommand(pairs);
            first.Add(open); first.Add(pairs);
            var undo = PanelCommand("table.doUndo", L("Geri topla", "Take back"), s.CanUndo, false, 40, 13, "undo", neutral: true);
            undo.tooltip = TableStrings.Get("undoTip", Lang); undo.Insert(0, view.Icon("v4/table-do-undo", 15, s.CanUndo ? "var(--a1)" : "var(--mt)"));
            var back = PanelCommand("table.doReturn", L("Geri bırak", "Put back"), s.ShowReturn, false, 40, 13, "putback", neutral: true);
            back.tooltip = TableStrings.Get("returnTip", Lang); back.Insert(0, view.Icon("v4/table-do-return", 15, s.ShowReturn ? "var(--a1)" : "var(--mt)"));
            // The 15 px svg is a flex item that may shrink: where the label fills the button ("Geri topla", 59 px of 65) the
            // browser squeezes the icon to nothing, so it does here too.
            foreach (var command in new[] { undo, back })
            {
                var icon = command[0]; icon.style.flexShrink = 1; icon.style.minWidth = 0; icon.style.overflow = Overflow.Hidden;
                command[1].style.flexShrink = 0;
            }
            second.Add(undo); second.Add(back);
            foreach (var action in new[] { open, pairs, undo, back }) { action.style.flexGrow = 1; action.style.flexBasis = 0; action.style.minWidth = 0; }
            grid.Add(first); grid.Add(second); panel.Add(grid);
            if (s.CanFromHand) BuildActionFinishCard(panel);
            hudLayer.Add(panel); FitActionPanel(panel);
            panel.schedule.Execute(() => FitActionPanel(panel)).Every(300);
        }

        private VisualElement PanelCommand(string name, string text, bool on, bool primary, int height, int size, string command, int shadow = 3, bool neutral = false)
        {
            var button = Row(6); button.name = name; button.pickingMode = PickingMode.Position; button.style.height = height; button.style.flexShrink = 0;
            button.style.justifyContent = Justify.Center; Pad(button, 0, 5, 0, 5); button.style.opacity = on ? 1 : .75f;
            string foreground = on ? neutral ? "#F4F1E8" : primary ? "var(--aT)" : "#05301A" : "var(--mt)";
            view.Decorate(button, on && !neutral ? primary ? "linear-gradient(var(--a1),var(--a2))" : "linear-gradient(#5BE38A,#2BB45F)" : "var(--pnH)", height == 46 ? 14 : 12, shadow, on && !neutral ? primary ? "var(--aS)" : "#18793F" : "var(--pnS)");
            button.Add(Text(text, "Montserrat-800", size, foreground)); button.focusable = on; button.SetEnabled(on);
            button.EnableInClassList("disabled", !on);
            if (on)
            {
                Clickable(button, command);
                button.RegisterCallback<NavigationSubmitEvent>(_ => Press(command));
            }
            return button;
        }

        private void WarnPanelCommand(VisualElement button)
        {
            view.Decorate(button, "linear-gradient(#FF9A6B,#E2553A)", 12, 3, "#8E2A16");
            foreach (var child in button.Children()) if (child is Label label) label.style.color = view.C("#fff");
            button.EnableInClassList("short", true);
        }

        private void PulsePanelCommand(VisualElement button, float seconds, Color color, string shadow = "var(--aS)")
        {
            GalaxyFilters.HardShadow(button, 5, view.C(shadow));
            var ring = GalaxyFilters.SpreadRing(button, 12);
            motions.Add(t =>
            {
                float phase = Mathf.Repeat(t, seconds) / seconds;
                float progress = MenuView.CssBezier(.25f, .1f, .25f, 1, phase < .5f ? phase * 2 : (phase - .5f) * 2);
                float spread = phase < .5f ? progress : 1 - progress;
                ring(spread * 12, new Color(color.r, color.g, color.b, color.a * (1 - spread)));
            });
        }

        private void BuildActionFinishCard(VisualElement panel)
        {
            var card = new VisualElement { name = "table.bombCard", pickingMode = PickingMode.Ignore }; card.style.marginTop = 8; Pad(card, 5, 5, 5, 5);
            view.Decorate(card, "rgba(255,106,46,.1)", 14); Border(card, 1, view.C("rgba(255,138,61,.32)"));
            var bomb = PanelCommand("table.doBomb", L("Elden bit", "Bomb out"), true, false, 40, 15, "fromhand");
            view.Decorate(bomb, "linear-gradient(#FF8A3D,#E2361F)", 11, 3, "#8E1F12"); bomb.userData = 9f; bomb.tooltip = TableStrings.Get("bombTip", Lang);
            foreach (var child in bomb.Children()) if (child is Label label) label.style.color = view.C("#fff");
            var icon = view.Icon("v4/table-do-bomb", 20, "#fff");
            icon.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(60)); bomb.Insert(0, icon);
            motions.Add(t =>
            {
                float phase = Mathf.Repeat(t, 1.6f) / 1.6f;
                float local = phase < .4f ? Mathf.Repeat(phase * 10, 1) : 0;
                float eased = MenuView.CssBezier(.42f, 0, .58f, 1, local);
                float rotation = phase < .1f ? Mathf.Lerp(0, -12, eased) : phase < .2f ? Mathf.Lerp(-12, 10, eased) : phase < .3f ? Mathf.Lerp(10, -6, eased) : phase < .4f ? Mathf.Lerp(-6, 0, eased) : 0;
                icon.style.rotate = new Rotate(rotation);
            });
            PulsePanelCommand(bomb, 1.4f, view.C("rgba(255,106,46,.7)"), "#8E1F12");
            card.Add(bomb);
            BuildActionFinishChoices(card); panel.Add(card);
        }

        private void BuildActionFinishChoices(VisualElement card)
        {
            string[] keys = { "nuke", "blackhole", "supernova", "meteor" }, tr = { "Nükleer", "Kara delik", "Süpernova", "Meteor" }, en = { "Nuke", "Black hole", "Supernova", "Meteor" };
            string[] paint = { "radial-gradient(circle at 50% 45%,#FFF6D8 0 18%,#FFB347 42%,#E2361F 64%,transparent 70%)", "radial-gradient(circle,#05030c 0 36%,#E6DDFF 42%,#B79CFF 50%,#3a2a90 64%,transparent 71%)", "radial-gradient(circle,#FFFFFF 0 22%,#CFEBFF 36%,#6FB8FF 56%,transparent 72%)", "radial-gradient(circle at 64% 36%,#FFF1C0 0 16%,#FF9A4A 38%,#B33A12 60%,transparent 70%)" };
            string[] glow = { "rgba(255,150,60,.8)", "rgba(183,156,255,.8)", "rgba(160,215,255,.9)", "rgba(255,120,60,.8)" };
            var choices = new VisualElement { name = "table.bombFx", pickingMode = PickingMode.Ignore }; choices.style.flexDirection = FlexDirection.Row;
            choices.style.flexWrap = Wrap.Wrap; choices.style.alignItems = Align.Center; choices.style.justifyContent = Justify.FlexEnd; choices.style.marginTop = 6; Pad(choices, 0, 2, 0, 6);
            string selected = Settings.Text("bombFx") ?? "nuke"; int selectedIndex = System.Array.IndexOf(keys, selected); if (selectedIndex < 0) selectedIndex = 0;
            var description = new VisualElement { pickingMode = PickingMode.Ignore }; description.style.flexGrow = 1; description.style.flexBasis = 90; description.style.minWidth = 0; description.style.marginRight = 5;
            var caption = Text(L("BİTİŞ EFEKTİ", "FINISH EFFECT"), "Baloo2-800", 8, "#FFB48A"); caption.name = "table.t.bombFxCap"; MenuView.Tracking(caption, 1.2f, 8); description.Add(caption);
            var effectName = Text(L(tr[selectedIndex], en[selectedIndex]), "Montserrat-800", 12, "#FFE2CC"); effectName.name = "table.bombFxName"; effectName.style.marginTop = 1; description.Add(effectName); choices.Add(description);
            var buttons = Row(5); buttons.style.flexShrink = 0; buttons.style.marginLeft = StyleKeyword.Auto;
            for (int i = 0; i < keys.Length; i++)
            {
                string key = keys[i]; bool on = i == selectedIndex;
                var button = new VisualElement { name = "table.bombFx[" + i + "]", focusable = true, tooltip = L(tr[i], en[i]) };
                button.style.width = button.style.height = 30; button.style.flexShrink = 0; button.style.alignItems = Align.Center; button.style.justifyContent = Justify.Center;
                button.style.translate = new Translate(0, 0);
                button.style.transitionProperty = new List<StylePropertyName> { new StylePropertyName("translate"), new StylePropertyName("background-color") };
                button.style.transitionDuration = new List<TimeValue> { new TimeValue(.15f, TimeUnit.Second) };
                button.style.transitionTimingFunction = new List<EasingFunction> { new EasingFunction(EasingMode.Ease) };
                view.Decorate(button, on ? "rgba(255,255,255,.16)" : "rgba(0,0,0,.22)", 9); if (on) Border(button, 2, view.C("#FFB48A")); button.EnableInClassList("selected", on);
                var orb = new VisualElement { pickingMode = PickingMode.Ignore }; orb.style.width = orb.style.height = 20; view.Decorate(orb, paint[i], 10); if (on) GalaxyFilters.Shadow(orb, 0, 0, i == 2 ? 6 : 5, view.C(glow[i])); button.Add(orb);
                System.Action pick = () => { Settings.Set("bombFx", key); Render(Current); };
                GalaxyFilters.PressSink(button);
                button.RegisterCallback<PointerEnterEvent>(_ => button.style.translate = new Translate(0, -2));
                button.RegisterCallback<PointerLeaveEvent>(_ => button.style.translate = new Translate(0, 0));
                button.RegisterCallback<ClickEvent>(_ => pick()); button.RegisterCallback<NavigationSubmitEvent>(_ => pick()); buttons.Add(button);
            }
            buttons.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                bool wrapped = buttons.layout.y >= description.layout.yMax - .5f;
                float gap = wrapped ? 5 : 0;
                if (!Mathf.Approximately(buttons.style.marginTop.value.value, gap)) buttons.style.marginTop = gap;
            });
            choices.Add(buttons); card.Add(choices);
        }
    }
}
