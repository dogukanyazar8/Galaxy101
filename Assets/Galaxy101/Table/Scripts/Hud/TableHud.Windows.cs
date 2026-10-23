using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Galaxy101.Table
{
    /// <summary>
    /// Table windows (spec/13, Faz 7): the settings window (T15–T18). They live on their own layer above the HUD and are
    /// rebuilt only when their own state changes, so game updates never reset a scroll position or a slider drag.
    /// </summary>
    public sealed partial class TableHud
    {
        /// <summary>Table settings; every change applies at once (spec/13 §6).</summary>
        public TableSettings Settings { get; private set; }

        /// <summary>Open window: null, "settings", "chat", "card" (<see cref="CardSeat"/>) or "gift".</summary>
        public string Window { get; private set; }
        public int SettingsTab { get; private set; }

        private VisualElement windowLayer;
        private readonly List<Action<float>> windowMotions = new List<Action<float>>();
        private ScrollView settingsScroll;
        private float settingsScrollY;

        private string S(string key, params object[] args) => TableStrings.Get(key, Lang, args);

        /// <summary>Commands the HUD answers itself (windows); the rest go to <see cref="Command"/>.</summary>
        private bool HandleLocal(string command)
        {
            if (HandleSocial(command) || HandleGifts(command)) return true;
            switch (command)
            {
                case "settings": OpenSettings(SettingsTab); return true;
                case "settings-close": CloseWindow(); return true;
                case "settings-reset": Settings.Reset(); return true;
            }
            if (command.StartsWith("settings-tab:", StringComparison.Ordinal)) { OpenSettings(int.Parse(command.Substring(13))); return true; }
            return false;
        }

        public void OpenSettings(int tab)
        {
            bool sameTab = Window == "settings" && SettingsTab == tab;
            Window = "settings"; SettingsTab = Mathf.Clamp(tab, 0, 3);
            if (!sameTab) settingsScrollY = 0;
            RenderWindows(sameTab);
        }

        public void CloseWindow()
        {
            if (Window == null) return;
            Window = null; CardSeat = 0; RenderWindows(false);
        }

        private void InitializeWindows(bool persist)
        {
            // The received-gift card and emoji sit under the windows (reference z-index 12 under the 42 of the settings).
            socialLayer = Layer("table-social"); canvas.Add(socialLayer);
            windowLayer = Layer("table-windows"); canvas.Add(windowLayer);
            canvas.schedule.Execute(() => { float now = Time.realtimeSinceStartup; for (int i = motions2.Count - 1; i >= 0; i--) if (!motions2[i](now)) motions2.RemoveAt(i); }).Every(16);
            Settings = new TableSettings(model, persist);
            ApplySceneSettings(null, 0);
            Settings.Changed += OnSettingChanged;
            FramePacing.DisplayChanged += OnDisplayChanged;
            // A table opened straight from the editor (no menu first) still uses the saved frame rate.
            if (persist && FramePacing.Choice != Settings.FrameRate) FramePacing.Apply(Settings.FrameRate);
        }

        private void UpdateWindows()
        {
            if (Window != null && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) CloseWindow();
            UpdateSocial(); UpdateNotice();
        }

        /// <summary>Camera view and the three skins (T19–T22). The game reacts to the same event for its own state.</summary>
        private void ApplySceneSettings(string key, float viewSeconds)
        {
            if ((key == null || key == "view") && table.CameraRig.Current != TableCameraRig.View.Lobby)
            {
                var target = PreferredView;
                if (table.CameraRig.Current != target) table.CameraRig.GoTo(target, viewSeconds);
            }
            if ((key == null || key == "tableSkin") && table.TableSkin != Settings.TableSkin) table.SetTableSkin(Settings.TableSkin);
            if ((key == null || key == "rackSkin") && table.RackSkin != Settings.RackSkin) table.SetRackSkin(Settings.RackSkin);
            if ((key == null || key == "tileSkin") && table.TileSkin != Settings.TileSkin) table.SetTileSkin(Settings.TileSkin);
        }

        /// <summary>Where the camera rests during play: "Oyuncu gözü" or "Kuşbakışı".</summary>
        public TableCameraRig.View PreferredView => Settings != null && Settings.View == "top" ? TableCameraRig.View.Top : TableCameraRig.View.Player;

        private static readonly HashSet<string> InPlaceKeys = new HashSet<string> { "autoSort", "highlight", "dbl", "master", "music", "sfx", "tileSnd", "chatSnd", "bubbles", "giftNotif", "emojiFx", "turnHint" };

        private void OnSettingChanged(string key)
        {
            ApplySceneSettings(key, 1.3f);
            if (key == null || key == "lang") { Render(Current); RenderToasts(); }
            // Toggles and sliders already show their new value (and a rebuild would cut a slider drag short).
            if (key == null || !InPlaceKeys.Contains(key)) RenderWindows(true);
        }

        private void RenderWindows(bool keepScroll)
        {
            if (windowLayer == null) return;
            if (keepScroll && settingsScroll != null) settingsScrollY = settingsScroll.scrollOffset.y;
            windowMotions.Clear(); windowLayer.Clear(); settingsScroll = null; chatLog = null; chatScroll = null; chatInput = null; cardAnchor = null;
            if (Window == "settings") BuildSettings(keepScroll);
            else if (Window == "chat") BuildChat();
            else if (Window == "card" && CardSeat > 0) BuildCard();
            else if (Window == "gift") BuildGifts();
            ApplyGaps(windowLayer);
        }

        private static VisualElement Tap(VisualElement e, Action action)
        {
            e.pickingMode = PickingMode.Position; e.focusable = true;
            e.RegisterCallback<ClickEvent>(evt => { action(); evt.StopPropagation(); });
            return e;
        }

        private static VisualElement Box(string name = null)
        {
            var e = new VisualElement { pickingMode = PickingMode.Ignore }; if (name != null) e.name = name; return e;
        }

        private static void Radius(VisualElement e, float r) => e.style.borderTopLeftRadius = e.style.borderTopRightRadius = e.style.borderBottomLeftRadius = e.style.borderBottomRightRadius = r;

        /// <summary>Column with a CSS gap (margin-top on every child after the first).</summary>
        private static void ColumnGap(VisualElement column, float gap)
        {
            for (int i = 1; i < column.childCount; i++) column[i].style.marginTop = gap;
        }

        /// <summary>modalIn .28s cubic-bezier(.2,1.2,.4,1): y +18 → 0, scale .95 → 1, with a small overshoot.</summary>
        private void ModalIn(VisualElement card)
        {
            float began = Time.realtimeSinceStartup;
            windowMotions.Add(t =>
            {
                float u = Mathf.Clamp01((t - began) / .28f), k = 1 + 2.70158f * Mathf.Pow(u - 1, 3) + 1.70158f * Mathf.Pow(u - 1, 2); // easeOutBack
                card.style.translate = new Translate(0, 18 * (1 - k)); card.style.scale = new Scale(Vector2.one * Mathf.LerpUnclamped(.95f, 1, k));
                card.style.opacity = Mathf.Clamp01(u * 4);
            });
        }

        // ---------- settings (T15–T18) ----------
        private static readonly string[] SettingsTabs = { "game", "sound", "look", "alerts" };

        private void BuildSettings(bool keepScroll)
        {
            var layer = Layer("table-settings-layer"); layer.pickingMode = PickingMode.Position; layer.style.alignItems = Align.Center; layer.style.justifyContent = Justify.Center;
            var dim = Layer("table-settings-dim"); dim.pickingMode = PickingMode.Position; dim.style.backgroundColor = new Color(0, 0, 0, .55f); GalaxyFilters.BackdropBlur(dim, 6);
            Tap(dim, CloseWindow); layer.Add(dim);
            windowLayer.Add(layer);

            var win = new VisualElement { name = "table-settings", pickingMode = PickingMode.Position };
            win.style.width = 920; win.style.height = 600; win.style.flexDirection = FlexDirection.Row;
            view.Decorate(win, "var(--pn)", 28, 10, "var(--pnS)"); Border(win, 1, new Color(1, 1, 1, .1f)); GalaxyFilters.Shadow(win, 0, 40, 45, new Color(0, 0, 0, .6f));
            win.RegisterCallback<ClickEvent>(evt => evt.StopPropagation());
            ModalIn(win); layer.Add(win);

            // Sidebar: 220 px, sd, padding 22 14, gap 6, a hairline on the right.
            var side = Box("table-settings-tabs"); side.style.width = 220; side.style.flexShrink = 0; Pad(side, 22, 14, 22, 14);
            side.style.backgroundColor = view.C("var(--sd)"); side.style.borderTopLeftRadius = side.style.borderBottomLeftRadius = 27;
            side.style.borderRightWidth = 1; side.style.borderRightColor = new Color(1, 1, 1, .06f);
            var head = Row(10); Pad(head, 0, 10, 16, 10);
            head.Add(view.Icon("settings", 26, "var(--a1)"));
            var title = Text(S("setTitle"), "Montserrat-700", 22, "#F4F1E8"); title.style.whiteSpace = WhiteSpace.Normal; title.style.flexShrink = 1; head.Add(title);
            side.Add(head);
            for (int i = 0; i < 4; i++)
            {
                int index = i; bool on = i == SettingsTab;
                var tab = Row(12); tab.name = "settings-tab-" + i; Pad(tab, 12, 14, 12, 14);
                view.Decorate(tab, on ? "var(--pnH)" : "transparent", 14);
                if (on) { var bar = Box(); MenuView.Place(bar, 0, 0, 3, 0); bar.style.bottom = 0; bar.style.height = StyleKeyword.Auto; bar.style.backgroundColor = view.C("var(--a1)"); bar.style.borderTopLeftRadius = bar.style.borderBottomLeftRadius = 14; tab.Add(bar); }
                else GalaxyFilters.HoverBrightness(tab, 1.25f);
                var dot = Box(); dot.style.width = dot.style.height = 8; dot.style.rotate = new Rotate(45); view.Decorate(dot, on ? "var(--a1)" : "rgba(255,255,255,.2)", 3); tab.Add(dot);
                tab.Add(Text(S("sTabs." + i + ".0"), "Montserrat-700", 17, on ? "#F4F1E8" : "var(--mt)"));
                Tap(tab, () => OpenSettings(index)); side.Add(tab);
            }
            ColumnGap(side, 6); head.style.marginTop = 0;
            var grow = Box(); grow.style.flexGrow = 1; side.Add(grow);
            var foot = Text(S("setFoot"), "Baloo2-700", 11, "var(--mt)"); foot.style.opacity = .7f; foot.style.whiteSpace = WhiteSpace.Normal; foot.style.flexShrink = 1; Pad(foot, 0, 12, 0, 12); side.Add(foot);
            win.Add(side);

            // Content: heading + close, the scrolling rows, footer with reset and Tamam.
            var content = Box(); content.style.flexGrow = 1; content.style.minWidth = 0; win.Add(content);
            var heading = Row(12); Pad(heading, 22, 28, 14, 28); heading.style.flexShrink = 0;
            var titles = Box(); titles.style.flexGrow = 1; titles.style.flexShrink = 1;
            titles.Add(Text(S("sTabs." + SettingsTab + ".0"), "Montserrat-700", 24, "#F4F1E8"));
            var sub = Text(S("sTabs." + SettingsTab + ".1"), "Baloo2-700", 13, "var(--mt)"); sub.style.marginTop = 2; titles.Add(sub);
            heading.Add(titles);
            var close = Box("settings-close"); close.style.width = close.style.height = 42; close.style.flexShrink = 0; close.style.alignItems = Align.Center; close.style.justifyContent = Justify.Center;
            view.Decorate(close, "var(--pnH)", 13, 4, "var(--pnS)"); GalaxyFilters.HoverBrightness(close, 1.25f); close.Add(view.Icon("close", 18, "var(--ic)"));
            Tap(close, CloseWindow); heading.Add(close);
            content.Add(heading);

            // The browser's thin scrollbar has no UI Toolkit twin; the list scrolls with the wheel or a drag.
            var scroll = new ScrollView(ScrollViewMode.Vertical) { name = "table-settings-list", horizontalScrollerVisibility = ScrollerVisibility.Hidden, verticalScrollerVisibility = ScrollerVisibility.Hidden };
            scroll.style.flexGrow = 1; scroll.style.flexShrink = 1; scroll.style.minHeight = 0; Pad(scroll.contentContainer, 4, 28, 18, 28);
            content.Add(scroll); settingsScroll = scroll;
            var list = scroll.contentContainer;
            switch (SettingsTab)
            {
                case 0:
                    ToggleRow(list, "autoSort"); ToggleRow(list, "highlight"); ToggleRow(list, "dbl");
                    string sec = Lang == "en" ? "s" : " sn";
                    SegmentRow(list, "warn", new[] { ("3" + sec, "3"), ("5" + sec, "5"), ("10" + sec, "10"), (L("Kapalı", "Off"), "0") }, Settings.WarnSeconds.ToString(), v => Settings.Set("warn", int.Parse(v)));
                    SegmentRow(list, "lang", new[] { ("Türkçe", "tr"), ("English", "en") }, Lang, v => Settings.Set("lang", v));
                    break;
                case 1:
                    SliderRow(list, "master"); SliderRow(list, "music"); SliderRow(list, "sfx");
                    ToggleRow(list, "tileSnd"); ToggleRow(list, "chatSnd");
                    break;
                case 2: BuildLookTab(list); break;
                case 3: ToggleRow(list, "bubbles"); ToggleRow(list, "giftNotif"); ToggleRow(list, "emojiFx"); ToggleRow(list, "turnHint"); break;
            }
            ColumnGap(list, 10);
            if (keepScroll && settingsScrollY > 0) { float y = settingsScrollY; scroll.schedule.Execute(() => scroll.scrollOffset = new Vector2(0, y)); }

            var footer = Row(12); Pad(footer, 14, 28, 20, 28); footer.style.flexShrink = 0; footer.style.borderTopWidth = 1; footer.style.borderTopColor = new Color(1, 1, 1, .06f);
            var reset = Text(S("reset"), "Montserrat-700", 14, "var(--mt)"); reset.name = "settings-reset"; Pad(reset, 8, 4, 8, 4);
            reset.style.whiteSpace = WhiteSpace.Normal; reset.style.maxWidth = 112; reset.style.flexShrink = 1;
            reset.RegisterCallback<PointerEnterEvent>(_ => reset.style.color = view.C("#F4F1E8")); reset.RegisterCallback<PointerLeaveEvent>(_ => reset.style.color = view.C("var(--mt)"));
            Tap(reset, () => Settings.Reset()); footer.Add(reset);
            var spacer = Box(); spacer.style.flexGrow = 1; footer.Add(spacer);
            var done = Text(S("done"), "Montserrat-700", 16, "var(--aT)"); done.name = "settings-done"; Pad(done, 12, 30, 12, 30);
            view.Decorate(done, "linear-gradient(var(--a1),var(--a2))", 15, 5, "var(--aS)"); GalaxyFilters.HoverBrightness(done, 1.08f);
            Tap(done, CloseWindow); footer.Add(done);
            content.Add(footer);
        }

        /// <summary>One settings row: padding 13 16, radius 16, dp, title 700 16 over a 700 12.5 muted line, control on the right.</summary>
        private VisualElement SettingRow(VisualElement list, string key, VisualElement control, string title = null, string description = null)
        {
            var row = Row(16); row.name = "setting-row-" + key; Pad(row, 13, 16, 13, 16); view.Decorate(row, "var(--dp)", 16);
            var labels = Box(); labels.style.flexGrow = 1; labels.style.flexShrink = 1; labels.style.minWidth = 0;
            labels.Add(Text(title ?? S("sRows." + key + ".0"), "Montserrat-700", 16, "#F4F1E8"));
            var desc = Text(description ?? S("sRows." + key + ".1"), "Baloo2-700", 12, "var(--mt)"); desc.style.whiteSpace = WhiteSpace.Normal; desc.style.flexShrink = 1; desc.style.marginTop = 2; labels.Add(desc);
            row.Add(labels); row.Add(control); list.Add(row); return row;
        }

        /// <summary>54×30 switch: green #3CCB6E when on, a white 24 px knob sliding 24 px (.2s).</summary>
        private void ToggleRow(VisualElement list, string key)
        {
            bool on = Settings.Bool(key);
            var toggle = Box("setting-" + key); toggle.style.width = 54; toggle.style.height = 30; toggle.style.flexShrink = 0;
            var knob = Box(); MenuView.Place(knob, 3, 3, 24, 24); view.Decorate(knob, "#fff", 12); GalaxyFilters.Shadow(knob, 0, 2, 2, new Color(0, 0, 0, .35f)); toggle.Add(knob);
            float shown = on ? 1 : 0, from = shown, began = -1;
            void Paint() { view.Decorate(toggle, on ? "#3CCB6E" : "rgba(255,255,255,.14)", 15); toggle.EnableInClassList("on", on); }
            Paint(); knob.style.translate = new Translate(24 * shown, 0);
            windowMotions.Add(t =>
            {
                if (began < 0) return;
                float u = Mathf.Clamp01((t - began) / .2f); shown = Mathf.LerpUnclamped(from, on ? 1 : 0, TableConstants.EaseOutCubic(u));
                knob.style.translate = new Translate(24 * shown, 0); if (u >= 1) began = -1;
            });
            Tap(toggle, () => { on = !on; from = shown; began = Time.realtimeSinceStartup; Paint(); Settings.Set(key, on); });
            SettingRow(list, key, toggle);
        }

        private void SegmentRow(VisualElement list, string key, (string Label, string Value)[] options, string current, Action<string> pick)
        {
            var group = Row(3); group.name = "setting-" + key; Pad(group, 3, 3, 3, 3); view.Decorate(group, "var(--sd)", 12); group.style.flexShrink = 0;
            foreach (var (label, value) in options)
            {
                bool on = value == current; string v = value;
                var option = Text(label, "Montserrat-700", 13, on ? "var(--aT)" : "var(--mt)"); option.name = "setting-" + key + "-" + value; Pad(option, 8, 13, 8, 13);
                view.Decorate(option, on ? "linear-gradient(var(--a1),var(--a2))" : "transparent", 9); option.EnableInClassList("selected", on);
                Tap(option, () => pick(v)); group.Add(option);
            }
            SettingRow(list, key, group);
        }

        private void SliderRow(VisualElement list, string key)
        {
            int value = Settings.Int(key);
            var box = Row(12); box.style.flexShrink = 0;
            var slider = new GalaxySlider(0, 100, value, view) { name = "setting-" + key }; slider.style.width = 200;
            var label = Text(value + "%", "Montserrat-700", 15, "#F4F1E8"); label.style.width = 44; label.style.unityTextAlign = TextAnchor.MiddleRight;
            slider.ValueChanged += x => { label.text = x + "%"; Settings.Set(key, x); };
            box.Add(slider); box.Add(label);
            SettingRow(list, key, box);
        }

        // ---------- Görünüm (T15, T19–T22) ----------
        private static readonly Dictionary<string, (string Rim, string Line, string Glow)> TablePreview = new Dictionary<string, (string, string, string)>
        {
            ["station"] = ("linear-gradient(160deg,#6a7396,#2c3352)", "var(--a1)", "var(--gl)"),
            ["wood"] = ("linear-gradient(160deg,#7A4528,#3A2014)", "#D9B15A", null),
            ["holo"] = ("linear-gradient(160deg,rgba(120,160,255,.35),rgba(20,30,60,.6))", "var(--a1)", "var(--gl)"),
            ["andromeda"] = ("linear-gradient(135deg,#1b1f4a,#6a4fc0 30%,#2f7ad6 55%,#c56fd0 80%,#1b1f4a)", "#B79CFF", "rgba(143,180,255,.55)"),
        };

        private static readonly Dictionary<string, (string Back, string Ledge, string Glow)> RackPreview = new Dictionary<string, (string, string, string)>
        {
            ["walnut"] = ("linear-gradient(#7a4527,#4a2614)", "#8a5230", null),
            ["sedef"] = ("linear-gradient(#4A1E12,#2a0f08)", "#E4BE62", null),
            ["chrome"] = ("linear-gradient(#c3c8d2,#7d8494)", "#e4e7ee", null),
            ["carbon"] = ("linear-gradient(#2c2f38,#14161d)", "#14161d", null),
            ["neon"] = ("linear-gradient(#0b0c14,#05060c)", "var(--a1)", "var(--gl)"),
            ["andromeda"] = ("linear-gradient(90deg,#0c0f2e,#3a2a6e 35%,#FFF1D0 50%,#3a6fd6 65%,#0c0f2e)", "#B79CFF", "rgba(143,180,255,.6)"),
        };

        private static readonly Dictionary<string, (string Face, string Edge, string Shade, string C1, string C2, string Joker, string Glow)> TilePreview = new Dictionary<string, (string, string, string, string, string, string, string)>
        {
            ["classic"] = ("linear-gradient(#FFFDF6,#F1E9D6)", "#E3D7BC", "#B9AB8C", "#D7263D", "#1F5FBF", "#127A4A", null),
            ["galaxy"] = ("linear-gradient(#3B2F7A,#1C1648)", "#120E30", "#0A0820", "#FF7A8E", "#FFD964", "#FFD964", "rgba(140,110,255,.5)"),
            ["gold"] = ("linear-gradient(#1d1d24,#050507)", "#D9B25A", "#8A6A20", "#FF4D5E", "#FFC83D", "#E9C46A", "rgba(255,201,74,.4)"),
            ["neon"] = ("linear-gradient(#14202E,#0A1018)", "#06090E", "#000000", "#3DB4FF", "#FF3D7F", "#3DF5E0", "rgba(61,245,224,.5)"),
            ["marble"] = ("linear-gradient(#FFFFFF,#E4E6EC)", "#C9CCD6", "#9499A8", "#2B3A55", "#C21F36", "#2B3A55", null),
        };

        /// <summary>
        /// Kare hızı (same setting as the menu's Görüntü tab): ‹ Otomatik · 144 Hz › over the options of the window's
        /// monitor; applies and saves at once.
        /// </summary>
        private void FrameRateRow(VisualElement list)
        {
            var options = new List<int>(FramePacing.Options(FramePacing.MaxHz, FramePacing.IsMobile));
            int current = Settings.FrameRate;
            if (!options.Contains(current)) { options.Add(current); options.Sort((a, b) => a == FramePacing.Auto ? -1 : b == FramePacing.Auto ? 1 : a == FramePacing.Unlimited ? 1 : b == FramePacing.Unlimited ? -1 : a.CompareTo(b)); }
            int index = options.IndexOf(current);
            var group = Row(3); group.name = "setting-fps"; Pad(group, 3, 3, 3, 3); view.Decorate(group, "var(--sd)", 12); group.style.flexShrink = 0;
            VisualElement Arrow(string glyph, string name, int step)
            {
                bool enabled = index + step >= 0 && index + step < options.Count;
                var arrow = Text(glyph, "Montserrat-700", 16, enabled ? "#F4F1E8" : "var(--mt)"); arrow.name = name; arrow.style.width = 34; arrow.style.unityTextAlign = TextAnchor.MiddleCenter; Pad(arrow, 6, 0, 6, 0);
                view.Decorate(arrow, "transparent", 9); arrow.EnableInClassList("at-end", !enabled); arrow.style.opacity = enabled ? 1 : .5f;
                if (enabled) GalaxyFilters.HoverBrightness(arrow, 1.3f);
                Tap(arrow, () => { if (enabled) Settings.Set("fps", options[index + step]); });
                return arrow;
            }
            var value = Text(FramePacing.Label(current, Lang, FramePacing.CurrentHz), "Montserrat-700", 14, "var(--aT)"); value.name = "setting-fps-value";
            value.style.minWidth = 150; value.style.unityTextAlign = TextAnchor.MiddleCenter; Pad(value, 8, 10, 8, 10); view.Decorate(value, "linear-gradient(var(--a1),var(--a2))", 9);
            group.Add(Arrow("‹", "setting-fps-prev", -1)); group.Add(value); group.Add(Arrow("›", "setting-fps-next", 1));
            SettingRow(list, "fps", group, L("Kare hızı", "Frame rate"), FramePacing.Description(current, Lang, FramePacing.MaxHz));
        }

        private void OnDisplayChanged() { if (Window == "settings") RenderWindows(true); }

        private void BuildLookTab(VisualElement list)
        {
            FrameRateRow(list);
            list.Add(LookCaption(S("lkView")));
            var views = CardGrid(2);
            for (int i = 0; i < TableSettings.Views.Length; i++)
            {
                string key = TableSettings.Views[i]; bool on = Settings.View == key;
                var card = Row(14); card.name = "setting-view-" + key; Pad(card, 12, 12, 12, 12); CardFace(card, on);
                var art = Box(); art.style.width = 96; art.style.height = 62; art.style.flexShrink = 0; art.style.overflow = Overflow.Hidden; view.Decorate(art, "var(--sd)", 11);
                if (key == "seat") SeatViewArt(art); else TopViewArt(art);
                card.Add(art);
                var words = Box(); words.style.flexShrink = 1; words.style.minWidth = 0;
                words.Add(Text(S("views." + i + ".0"), "Montserrat-700", 16, "#F4F1E8"));
                var desc = Text(S("views." + i + ".1"), "Baloo2-700", 12, "var(--mt)"); desc.style.whiteSpace = WhiteSpace.Normal; desc.style.flexShrink = 1; words.Add(desc);
                card.Add(words);
                Tap(card, () => Settings.Set("view", key)); AddCell(views, card);
            }
            list.Add(views);

            list.Add(LookCaption(S("lkTable")));
            var tables = CardGrid(4);
            for (int i = 0; i < TableAssetCatalog.TableSkins.Length; i++)
            {
                string key = TableAssetCatalog.TableSkins[i]; var p = TablePreview[key];
                var art = PreviewWell();
                var rim = Box(); rim.style.width = 104; rim.style.height = 64; Pad(rim, 6, 6, 6, 6); view.Decorate(rim, p.Rim, 16);
                if (p.Glow != null) GalaxyFilters.Shadow(rim, 0, 0, 9, view.C(p.Glow)); else GalaxyFilters.HardShadow(rim, 3, view.C("#22120a"));
                if (key == "station") Border(rim, 2, view.C("var(--a1)"));
                var felt = Box(); felt.style.flexGrow = 1; view.Decorate(felt, key == "andromeda" ? "radial-gradient(#141a46,#04050f)" : key == "holo" ? "radial-gradient(var(--f2),#03050d)" : "radial-gradient(var(--f1),var(--f2))", 12);
                Border(felt, key == "holo" || key == "andromeda" ? 1 : 2, view.C(p.Line));
                if (key == "holo") for (int g = 1; g < 6; g++) { var line = Box(); MenuView.Place(line, g * 15, 0, 1, 52); line.style.backgroundColor = new Color(1, 1, 1, .08f); felt.Add(line); }
                if (key == "andromeda") { var core = Box(); MenuView.Place(core, 22, 17, 44, 14); view.Decorate(core, "radial-gradient(#FFF6E0,#F5C98A 30%,rgba(143,176,255,.75) 62%,transparent 80%)", 7); felt.Add(core); }
                rim.Add(felt); art.Add(rim);
                AddCell(tables, SkinCard("tableSkin", key, art, S("tSkins." + i + ".0"), S("tSkins." + i + ".1"), Settings.TableSkin == key));
            }
            list.Add(tables);

            list.Add(LookCaption(S("lkRack")));
            var racks = CardGrid(3);
            for (int i = 0; i < TableAssetCatalog.RackSkins.Length; i++)
            {
                string key = TableAssetCatalog.RackSkins[i]; var p = RackPreview[key];
                var art = PreviewWell();
                var back = Box(); Pad(back, 5, 7, 4, 7); view.Decorate(back, p.Back, 7);
                if (p.Glow != null) GalaxyFilters.Shadow(back, 0, 0, 7, view.C(p.Glow));
                foreach (var shelf in new[] { new[] { ("7", "#D7263D"), ("8", "#D7263D"), ("9", "#D7263D"), (null, null), ("4", "#1F5FBF") }, new[] { ("12", "#1B1B1B"), ("12", "#E39B12"), ("12", "#1F5FBF"), (null, null), ("5", "#E39B12") } })
                {
                    var row = Row(3); Pad(row, 0, 0, 3, 0); row.style.borderBottomWidth = 3; row.style.borderBottomColor = view.C(p.Ledge);
                    foreach (var (n, c) in shelf)
                    {
                        var tile = Box(); tile.style.width = 14; tile.style.height = 20; tile.style.alignItems = Align.Center; tile.style.justifyContent = Justify.Center;
                        if (n != null) { view.Decorate(tile, "linear-gradient(var(--tf1),var(--tf2))", 3); tile.Add(Text(n, "Montserrat-800", 10, c)); }
                        row.Add(tile);
                    }
                    back.Add(row);
                }
                ColumnGap(back, 3); art.Add(back);
                AddCell(racks, SkinCard("rackSkin", key, art, S("rSkins." + i + ".0"), S("rSkins." + i + ".1"), Settings.RackSkin == key));
            }
            list.Add(racks);

            list.Add(LookCaption(S("lkTile")));
            var styles = CardGrid(5);
            for (int i = 0; i < TableAssetCatalog.TileSkins.Length; i++)
            {
                string key = TableAssetCatalog.TileSkins[i]; var p = TilePreview[key];
                var art = PreviewWell(); art.style.flexDirection = FlexDirection.Row;
                foreach (var (glyph, color) in new[] { ("7", p.C1), ("7", p.C2), ("★", p.Joker) })
                {
                    var tile = Box(); tile.style.width = 26; tile.style.height = 38; tile.style.marginLeft = tile.style.marginRight = 2.5f; tile.style.alignItems = Align.Center; tile.style.justifyContent = Justify.Center;
                    view.Decorate(tile, p.Face, 5, 2, p.Shade); tile.style.borderBottomWidth = 3; tile.style.borderBottomColor = view.C(p.Edge);
                    if (p.Glow != null) GalaxyFilters.Shadow(tile, 0, 0, 5, view.C(p.Glow));
                    tile.Add(Text(glyph, "Montserrat-800", glyph == "★" ? 15 : 17, color));
                    if (glyph != "★") { var dot = Box(); dot.style.width = dot.style.height = 5; dot.style.marginTop = 1; view.Decorate(dot, color, 3); tile.Add(dot); }
                    art.Add(tile);
                }
                AddCell(styles, SkinCard("tileSkin", key, art, S("tlSkins." + i + ".0"), S("tlSkins." + i + ".1"), Settings.TileSkin == key));
            }
            list.Add(styles);
        }

        private VisualElement LookCaption(string text)
        {
            var label = Text(text, "Baloo2-800", 10, "var(--mt)"); MenuView.Tracking(label, 1.6f, 10); label.style.marginTop = 2; return label;
        }

        /// <summary>CSS grid repeat(n, minmax(0,1fr)) with a 10 px gap: rows of n equal cells (a short last row keeps the width).</summary>
        private static VisualElement CardGrid(int columns)
        {
            var grid = Box(); grid.userData = columns; return grid;
        }

        private static void AddCell(VisualElement grid, VisualElement cell)
        {
            int columns = (int)grid.userData;
            VisualElement row = grid.childCount > 0 ? grid[grid.childCount - 1] : null;
            if (row == null || (int)row.userData == columns)
            {
                row = Box(); row.style.flexDirection = FlexDirection.Row; row.style.alignItems = Align.Stretch;
                if (grid.childCount > 0) row.style.marginTop = 10;
                grid.Add(row);
                for (int i = 0; i < columns; i++) { var slot = Box(); slot.style.flexGrow = 1; slot.style.flexBasis = 0; slot.style.minWidth = 0; if (i > 0) slot.style.marginLeft = 10; row.Add(slot); }
                row.userData = 0;
            }
            int index = (int)row.userData; row.userData = index + 1;
            var holder = row[index]; holder.Add(cell); cell.style.flexGrow = 1;
        }

        private VisualElement PreviewWell()
        {
            var well = Box(); well.style.height = 92; well.style.alignItems = Align.Center; well.style.justifyContent = Justify.Center; view.Decorate(well, "var(--sd)", 12); return well;
        }

        /// <summary>Selected card: pnH with "inset 0 0 0 2px var(--a1), 0 0 20px var(--gl)"; others dp.</summary>
        private void CardFace(VisualElement card, bool on)
        {
            view.Decorate(card, on ? "var(--pnH)" : "var(--dp)", 16);
            card.EnableInClassList("selected", on);
            if (on) { Border(card, 2, view.C("var(--a1)")); GalaxyFilters.Shadow(card, 0, 0, 10, view.C("var(--gl)")); }
            else { Border(card, 2, Color.clear); GalaxyFilters.HoverBrightness(card, 1.12f); }
        }

        private VisualElement SkinCard(string setting, string key, VisualElement art, string name, string desc, bool on)
        {
            var card = Box("setting-" + setting + "-" + key); Pad(card, 10, 10, 10, 10); CardFace(card, on);
            card.Add(art);
            var title = Text(name, "Montserrat-700", 15, "#F4F1E8"); title.style.marginTop = 10; title.style.overflow = Overflow.Hidden; title.style.textOverflow = TextOverflow.Ellipsis; title.style.flexShrink = 1; card.Add(title);
            var line = Text(desc, "Baloo2-700", 12, "var(--mt)"); line.style.whiteSpace = WhiteSpace.Normal; line.style.flexShrink = 1; line.style.marginTop = 2; card.Add(line);
            if (on)
            {
                var tag = Text(S("inUse"), "Baloo2-800", 9, "var(--aT)"); MenuView.Tracking(tag, 1, 9); tag.name = "setting-in-use";
                tag.style.position = Position.Absolute; tag.style.right = 16; tag.style.top = 16; Pad(tag, 3, 7, 3, 7); view.Decorate(tag, "var(--a1)", 6); card.Add(tag);
            }
            Tap(card, () => Settings.Set(setting, key));
            return card;
        }

        /// <summary>"Oyuncu gözü" sketch (96×62 SVG): table trapezoid, rim bar, seven tiles, a gold dot opposite.</summary>
        private void SeatViewArt(VisualElement art)
        {
            var felt = Box(); MenuView.Place(felt, 22, 16, 52, 30); view.Decorate(felt, "var(--f1)", 3); Border(felt, 1.5f, view.C("var(--a1)"));
            felt.style.scale = new Scale(new Vector2(1.45f, 1)); felt.style.transformOrigin = new TransformOrigin(Length.Percent(50), Length.Percent(100));
            art.Add(felt);
            var rim = Box(); MenuView.Place(rim, 16, 46, 64, 10); view.Decorate(rim, "var(--rim)", 3); art.Add(rim);
            for (int i = 0; i < 7; i++) { var t = Box(); MenuView.Place(t, 20 + i * 8, 40, 6, 8); view.Decorate(t, "var(--tf1)", 2); art.Add(t); }
            var dot = Box(); MenuView.Place(dot, 44, 4, 8, 8); view.Decorate(dot, "var(--a1)", 4); art.Add(dot);
        }

        /// <summary>"Kuşbakışı" sketch: square table from above with the four racks around it and two tiles.</summary>
        private void TopViewArt(VisualElement art)
        {
            var felt = Box(); MenuView.Place(felt, 24, 5, 48, 48); view.Decorate(felt, "var(--f1)", 8); Border(felt, 1.5f, view.C("var(--a1)")); art.Add(felt);
            foreach (var (x, y, w, h) in new[] { (30, 54, 36, 6), (15, 15, 6, 28), (75, 15, 6, 28), (30, 0, 36, 4) })
            { var r = Box(); MenuView.Place(r, x, y, w, h); view.Decorate(r, "var(--rim)", 2); art.Add(r); }
            foreach (var x in new[] { 42, 49 }) { var t = Box(); MenuView.Place(t, x, 25, 5, 7); view.Decorate(t, "var(--tf1)", 1); art.Add(t); }
        }
    }
}
