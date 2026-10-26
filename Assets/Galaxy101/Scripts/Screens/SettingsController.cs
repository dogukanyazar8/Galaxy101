using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    public sealed class SettingsController
    {
        private JObject draft;
        private MenuView owner;
        private readonly SettingsMasa masa = new SettingsMasa();
        // D-62 ids of the rows, numbered per kind within a tab as the design's lists are (setToggles, setSliders, setSegs).
        private int toggles, sliders, segments;
        /// <summary>The sidebar order of the tabs (G101-Menu setTabs).</summary>
        public static readonly int[] Order = { 0, 1, 2, 5, 3, 4 };
        public static string[] Titles(MenuView v) => new[] { v.L("Oyun", "Game"), v.L("Ses", "Sound"), v.L("Görüntü", "Display"), v.L("Dil", "Language"), v.L("Hesap", "Account"), "Masa" };   // SET_EN has no entry for the Masa tab: the prototype shows "Masa" in English too (EKSIKLER EK-006)
        public static string[] Subtitles(MenuView v) => new[]
        {
            v.L("Taş hareketleri ve masa davranışı", "Tile movement and table behavior"),
            v.L("Müzik, efektler ve bildirim sesleri", "Music, effects and notification sounds"),
            v.L("Tema, çözünürlük ve görsel efektler", "Theme, resolution and visual effects"),
            v.L("Uygulamanın dili", "App language"),
            v.L("Profil, bağlı hesaplar ve bildirimler", "Profile, linked accounts and notifications"),
            "Görüş açısı ve masa görünümü"   // untranslated in the prototype too (EK-006)
        };

        public void OnOpen(MenuView view)
        {
            owner = view;
            draft = view.Services.Settings.Load();
        }

        public void Discard()
        {
            owner?.PreviewSettings(null);
            draft = null;
        }

        public void Cancel(MenuView view) { Discard(); view.CloseTop(); }

        public VisualElement Build(MenuView v)
        {
            if (draft == null) OnOpen(v);
            var root = PanelUi.Shell(v, "Settings");
            var tab = v.Tab("settings");
            var sidebar = root.Q<VisualElement>("sidebar");
            // Reference: padding 0 10px 16px, 26px gear, gap 10, 700 26px/1.1 heading.
            var titleText = v.Text(v.L("Ayarlar", "Settings"), 26, true); titleText.name = "menu.t.settings"; titleText.style.height = 28.6f; titleText.style.unityTextAlign = TextAnchor.MiddleLeft; titleText.style.whiteSpace = WhiteSpace.NoWrap;
            var title = v.Row(v.Icon("settings", 26, "a1"), titleText); PanelUi.Gap(title, 10); title.AddToClassList("g-settings-title"); sidebar.Add(title);
            // G101-Menu settingsVals: TABS 0 Oyun, 1 Ses, 2 Görüntü, 3 Dil, 4 Hesap, 5 Masa; the sidebar lists them as setTabs [0, 1, 2, 5, 3, 4].
            var titles = Titles(v);
            for (var k = 0; k < Order.Length; k++)
            {
                var index = Order[k];
                var button = PanelUi.CustomButton(v, "", () => v.SetTab("settings", index), "g-settings-nav");
                button.style.flexDirection = FlexDirection.Row; button.style.justifyContent = Justify.FlexStart;
                // The active bar is an inset box-shadow in the reference, so it must not move the label like a border would.
                if (index == tab)
                {
                    var bar = v.Box("g-settings-nav-bar"); bar.pickingMode = PickingMode.Ignore; v.Decorate(bar, "a1", 14);
                    var face = v.Box("g-settings-nav-face"); face.pickingMode = PickingMode.Ignore; v.Decorate(face, "pnH", 14); bar.Add(face); button.Add(bar);
                }
                var diamond = v.Box("g-settings-nav-dot"); diamond.style.width = diamond.style.height = 8; diamond.style.rotate = new Rotate(45); diamond.style.marginRight = 12;
                v.Decorate(diamond, index == tab ? "a1" : "bd", 3); button.Add(diamond);
                var label = v.Text(titles[index], 17, true); label.name = "menu.t.name[" + k + "]"; label.style.color = StyleKeyword.Null; label.style.height = 20.4f; label.style.unityTextAlign = TextAnchor.MiddleLeft; label.style.whiteSpace = WhiteSpace.NoWrap; button.Add(label);
                button.EnableInClassList("selected", index == tab); button.name = "menu.t.pick[" + k + "]"; sidebar.Add(button);
            }
            sidebar.Add(PanelUi.Spacer(v));
            // menu.askQuit: padding 12px 14px, radius 14, #4A2236 inside a 1px rgba(255,94,108,.35) ring on a 4px #2A1220
            // ledge, #FF9AA3; the 20px power icon, gap 12, 700 17px/1.2 heading; hover brightness 1.2.
            var quit = PanelUi.CustomButton(v, "", () => v.OpenDialog("quit"), "g-settings-quit bright-120"); quit.name = "menu.askQuit"; quit.tooltip = v.L("Galaxy 101’i kapat", "Close Galaxy 101");
            quit.style.flexDirection = FlexDirection.Row; quit.style.justifyContent = Justify.FlexStart; quit.style.alignItems = Align.Center; PanelUi.Pad(quit, 12, 14); v.Decorate(quit, "#4A2236", 14, 4, "#2A1220");
            quit.Add(PanelUi.Ring(v, 1, "rgba(255,94,108,.35)", 14));
            var power = v.Icon("v4/menu-ask-quit", 20, "#FF9AA3"); power.style.marginRight = 12; quit.Add(power);
            var quitLabel = v.Text(v.L("Oyundan çık", "Quit game"), 17, true, "#FF9AA3"); quitLabel.name = "menu.quitLabel"; quitLabel.style.height = 20.4f; quitLabel.style.unityTextAlign = TextAnchor.MiddleLeft; quitLabel.style.whiteSpace = WhiteSpace.NoWrap; quit.Add(quitLabel);
            sidebar.Add(quit);
            var content = root.Q<VisualElement>("content");
            var subtitles = Subtitles(v);
            var tabTitle = v.Text(titles[tab], 24, true); tabTitle.name = "menu.tabTitle";
            var tabSub = v.Text(subtitles[tab], 13, false, "mt"); tabSub.name = "menu.tabSub"; tabSub.style.marginTop = 2; tabSub.style.whiteSpace = WhiteSpace.NoWrap; tabSub.style.overflow = Overflow.Hidden; tabSub.style.textOverflow = TextOverflow.Ellipsis;
            var headingText = v.Column(tabTitle, tabSub); PanelUi.Grow(headingText);
            var heading = v.Row(headingText); heading.AddToClassList("g-settings-heading");
            var close = v.Button("", () => Cancel(v), "secondary"); close.name = "menu.closeSettings#2"; close.tooltip = v.L("Kapat", "Close");
            close.style.width = close.style.height = 42; close.style.minWidth = 42; close.style.paddingLeft = close.style.paddingRight = 0; close.style.marginLeft = 12; close.Add(v.Icon("close", 18)); heading.Add(close); content.Add(heading);
            var scroll = v.Scroll(); scroll.AddToClassList("g-settings-list"); content.Add(scroll); toggles = sliders = segments = 0;
            switch (tab)
            {
                case 0:
                    Toggle(v, scroll, "autoSort", v.L("Otomatik sırala", "Auto-sort"), v.L("Yeni çekilen taş ıstakada uygun yere yerleşsin", "Drawn tiles go to the right spot on your rack"));
                    Toggle(v, scroll, "highlight", v.L("Perleri vurgula", "Highlight sets"), v.L("Geçerli perlerin çevresi yeşil yansın", "Valid sets glow green"));
                    Toggle(v, scroll, "dbl", v.L("Çift tıkla taş at", "Double-click to discard"), v.L("Sürüklemeden hızlı atış", "Discard quickly without dragging"));
                    Toggle(v, scroll, "chat", v.L("Masa sohbeti", "Table chat"), v.L("Masa içi mesaj ve emojiler görünsün", "Show messages and emojis at the table"));
                    Slider(v, scroll, "warn", v.L("Tur süresi uyarısı", "Turn timer warning"), v.L("Süre bitmeden kaç saniye önce uyarılsın", "How many seconds before your time runs out"), 1, 10, " sn"); // sl('warn', …, ' sn'): the unit is not in SET_EN, English shows "5 sn" too (EK-611)
                    Segment(v, scroll, "tile", v.L("Taş boyutu", "Tile size"), v.L("Istakadaki taşların büyüklüğü", "Size of the tiles on your rack"), new[] { "Küçük", "Orta", "Büyük" });
                    break;
                case 1:
                    Toggle(v, scroll, "tileSnd", v.L("Taş sesleri", "Tile sounds"), v.L("Taş çekme ve atma sesi", "Sound when drawing and discarding"));
                    Toggle(v, scroll, "notifSnd", v.L("Bildirim sesi", "Notification sound"), v.L("Sıra sende ve davet uyarıları", "Your-turn and invite alerts"));
                    Slider(v, scroll, "master", v.L("Ana ses", "Master volume"), v.L("Tüm seslerin genel seviyesi", "Overall level of all sounds"), 0, 100, "%");
                    Slider(v, scroll, "music", v.L("Müzik", "Music"), v.L("Menü ve masa müziği", "Menu and table music"), 0, 100, "%");
                    Slider(v, scroll, "sfx", v.L("Efektler", "Effects"), v.L("Taş, zar ve buton sesleri", "Tile, dice and button sounds"), 0, 100, "%");
                    break;
                case 2:
                    Toggle(v, scroll, "motion", v.L("Arka plan hareketi", "Background motion"), v.L("Dönen gezegen ve kayan yıldızlar", "Spinning planet and shooting stars"), true);
                    // spec/19 (S07): görüntü modu, çözünürlük (monitöre göre), kare hızı; the planet row moved to the main menu picker.
                    DisplayMode(v, scroll);
                    Resolution(v, scroll);
                    FrameRate(v, scroll);
                    Segment(v, scroll, "quality", v.L("Efekt kalitesi", "Effect quality"), v.L("Düşük ayar eski bilgisayarlarda akıcılığı artırır", "Lower settings run smoother on older computers"), new[] { "Düşük", "Orta", "Yüksek" });
                    CursorRow(v, scroll);
                    break;
                case 3: Languages(v, scroll); break;
                case 4: Account(v, scroll); break;
                case 5: masa.Build(v, scroll); break;
            }
            var footer = v.Row(); footer.AddToClassList("g-settings-footer");
            var reset = PanelUi.CustomButton(v, v.L("Varsayılana dön", "Reset to default"), () =>
            {
                draft = v.Services.Settings.ResetToDefaults();
                if (!v.Services.Settings.SetLanguage((string)draft["lang"])) { draft["lang"] = v.Model.Settings["lang"]; PanelUi.SaveFailure(v); }
                if (!v.Services.Settings.SetDisplay((string)draft["wmode"], (string)draft["res"])) { draft["wmode"] = v.Model.Settings["wmode"]; draft["res"] = v.Model.Settings["res"]; PanelUi.SaveFailure(v); }
                if (!v.Services.Settings.SetFrameRate((int)draft["fps"])) { draft["fps"] = v.Model.Settings["fps"]; PanelUi.SaveFailure(v); }
                v.PreviewSettings(draft); v.Refresh();
            }, "");
            reset.name = "menu.resetSettings"; reset.style.backgroundColor = UnityEngine.Color.clear; reset.style.color = v.C("mt"); reset.style.fontSize = 14;
            reset.style.paddingLeft = reset.style.paddingRight = 0; reset.style.unityFont = v.Text("", 14).style.unityFont;
            footer.Add(reset); footer.Add(PanelUi.Spacer(v));
            var cancel = PanelUi.CssButton(v, v.L("Vazgeç", "Cancel"), () => Cancel(v), "secondary bright-120", 12, 22, 16); cancel.name = "menu.closeSettings#3"; footer.Add(cancel);
            var save = PanelUi.CssButton(v, v.L("Kaydet", "Save"), () =>
            {
                if (!v.Services.Settings.Save(draft)) { PanelUi.SaveFailure(v); return; }
                draft = null; v.CloseTop();
            }, "primary", 12, 28, 16);
            save.name = "menu.closeSettings#4"; save.style.marginLeft = 12; footer.Add(save); content.Add(footer);
            return root;
        }

        private static VisualElement SettingRow(MenuView v, VisualElement list, string title, string description, VisualElement control, string kind = null, int index = 0)
        {
            var name = v.Text(title, 17, true); var desc = PanelUi.Wrap(v, description, 13);
            if (kind != null) { name.name = "menu." + kind + ".name[" + index + "]"; desc.name = "menu." + kind + ".desc[" + index + "]"; }
            var labels = v.Column(name, desc); PanelUi.Grow(labels); labels.style.marginRight = 16;
            var row = v.Row(labels, control); row.AddToClassList("g-settings-row"); list.Add(row); return row;
        }

        private void Toggle(MenuView v, VisualElement list, string key, string title, string description, bool preview = false)
        {
            var on = (bool)draft[key]; Button button = null;
            button = PanelUi.CustomButton(v, "", () =>
            {
                on = !on; draft[key] = on; button.EnableInClassList("on", on);
                if (preview) v.PreviewSettings(draft);
            }, "g-toggle");
            button.name = "setting-" + key; button.EnableInClassList("on", on);
            button.Add(v.Box("g-toggle-knob")); SettingRow(v, list, title, description, button, "t", toggles++);
        }

        private void Slider(MenuView v, VisualElement list, string key, string title, string description, int min, int max, string unit)
        {
            var value = (int)draft[key]; var label = v.Text(value + unit, 15, true); label.AddToClassList("g-slider-value"); label.name = "menu.s.label[" + sliders + "]";
            PanelUi.Pad(label, 6, 0);
            label.style.color = v.C(value == min ? "mt" : "#F4F1E8");
            var slider = new GalaxySlider(min, max, value, v); slider.name = "setting-" + key;
            slider.style.marginRight = 18;
            slider.ValueChanged += x => { draft[key] = x; label.text = x + unit; label.style.color = v.C(x == min ? "mt" : "#F4F1E8"); };
            var row = v.Row(slider, label); row.style.flexShrink = 0; row.style.marginLeft = 2;
            SettingRow(v, list, title, description, row, "s", sliders++);
        }

        private void Segment(MenuView v, VisualElement list, string key, string title, string description, string[] values)
        {
            var group = v.Row(); group.AddToClassList("g-segment");
            for (var i = 0; i < values.Length; i++)
            {
                var option = values[i];
                var button = PanelUi.CustomButton(v, v.L(option), () => { draft[key] = option; v.Refresh(); }, "g-segment-button"); button.name = "menu.o.pick[" + i + "]";
                button.EnableInClassList("selected", (string)draft[key] == option);
                if ((string)draft[key] == option) v.Decorate(button, "linear-gradient(var(--a1),var(--a2))", 10, 3, "aS");
                group.Add(button);
            }
            PanelUi.Gap(group, 4);
            SettingRow(v, list, title, description, group, "g", segments++);
        }

        /// <summary>
        /// İmleç (cursorRow): Normal / Büyük / Sistem — the Galaxy 101 cursor in the theme colour, or the system's. The
        /// design names this row in Turkish only (cursorRow is not in SET_EN).
        /// </summary>
        private void CursorRow(MenuView v, VisualElement list)
        {
            var group = v.Row(); group.AddToClassList("g-segment"); group.name = "setting-cursor"; var current = Galaxy101.Audio.GalaxyCursor.Mode;
            for (var i = 0; i < Galaxy101.Audio.GalaxyCursor.Modes.Length; i++)
            {
                var mode = Galaxy101.Audio.GalaxyCursor.Modes[i];
                var button = PanelUi.CustomButton(v, mode, () => { Galaxy101.Audio.GalaxyCursor.Mode = mode; v.Refresh(); }, "g-segment-button"); button.name = "menu.o.pick[" + i + "]";
                button.EnableInClassList("selected", mode == current);
                if (mode == current) v.Decorate(button, "linear-gradient(var(--a1),var(--a2))", 10, 3, "aS");
                group.Add(button);
            }
            PanelUi.Gap(group, 4);
            var row = SettingRow(v, list, "İmleç", current == "Sistem" ? "İşletim sisteminin imleci kullanılır" : "Galaxy 101 imleci · tema rengini izler", group, "g", segments++);
            var desc = row.Query<Label>().AtIndex(1); desc.style.whiteSpace = WhiteSpace.NoWrap; desc.style.overflow = Overflow.Hidden; desc.style.textOverflow = TextOverflow.Ellipsis;
        }

        /// <summary>Görüntü modu: Tam ekran (exclusive) / Çerçevesiz / Pencere; applied and saved at once.</summary>
        private void DisplayMode(MenuView v, VisualElement list)
        {
            var group = v.Row(); group.AddToClassList("g-segment"); group.name = "setting-wmode";
            var labels = new[] { v.L("Tam ekran", "Fullscreen"), v.L("Çerçevesiz", "Borderless"), v.L("Pencere", "Windowed") };
            for (var i = 0; i < GameDisplay.ModeKeys.Length; i++)
            {
                string mode = GameDisplay.ModeKeys[i]; bool on = (string)draft["wmode"] == mode;
                var button = PanelUi.CustomButton(v, labels[i], () =>
                {
                    draft["wmode"] = mode;
                    if (!v.Services.Settings.SetDisplay(mode, (string)draft["res"])) { draft["wmode"] = v.Model.Settings["wmode"]; PanelUi.SaveFailure(v); }
                    v.Refresh();
                }, "g-segment-button");
                button.name = "setting-wmode-" + mode; button.EnableInClassList("selected", on);
                if (on) v.Decorate(button, "linear-gradient(var(--a1),var(--a2))", 10, 3, "aS");
                group.Add(button);
            }
            PanelUi.Gap(group, 4);
            var row = SettingRow(v, list, v.L("Görüntü modu", "Display mode"), v.L("Tam ekran, çerçevesiz pencere veya pencere", "Fullscreen, borderless or windowed"), group, "g", segments++);
            var desc = row.Query<Label>().AtIndex(1); desc.style.whiteSpace = WhiteSpace.NoWrap; desc.style.overflow = Overflow.Hidden; desc.style.textOverflow = TextOverflow.Ellipsis;
        }

        /// <summary>Çözünürlük: ‹ 1920×1080 › over the standard sizes up to the monitor's native one (the default).</summary>
        private void Resolution(MenuView v, VisualElement list)
        {
            var native = GameDisplay.NativeSize; var options = GameDisplay.OptionsFor((string)draft["wmode"]);
            var current = GameDisplay.Resolve((string)draft["res"], native);
            // Tam ekran without that mode: the size the monitor actually switches to.
            if (!options.Contains(current)) current = options.Where(o => o.x <= current.x && o.y <= current.y).DefaultIfEmpty(native).Last();
            int index = Mathf.Max(0, options.ToList().IndexOf(current));
            string desc = (current == native ? v.L("Yerel · ", "Native · ") : "") + v.L("Monitör en fazla ", "Monitor max ") + GameDisplay.Label(native);
            Stepper(v, list, "res", v.L("Çözünürlük", "Resolution"), desc, GameDisplay.Label(current), index, options.Count, next =>
            {
                string value = options[next] == native ? GameDisplay.Native : GameDisplay.Format(options[next]);
                draft["res"] = value;
                if (!v.Services.Settings.SetDisplay((string)draft["wmode"], value)) { draft["res"] = v.Model.Settings["res"]; PanelUi.SaveFailure(v); }
            });
        }

        /// <summary>Kare hızı: ‹ Otomatik · 144 Hz › over the monitor's rates (spec/19).</summary>
        private void FrameRate(MenuView v, VisualElement list)
        {
            var options = new List<int>(FramePacing.Options(FramePacing.MaxHz, FramePacing.IsMobile));
            int current = (int)draft["fps"];
            if (!options.Contains(current)) { options.Add(current); options.Sort((a, b) => a == FramePacing.Auto ? -1 : b == FramePacing.Auto ? 1 : a == FramePacing.Unlimited ? 1 : b == FramePacing.Unlimited ? -1 : a.CompareTo(b)); }
            Stepper(v, list, "fps", v.L("Kare hızı", "Frame rate"), FramePacing.Description(current, v.Loc.Language, FramePacing.MaxHz),
                FramePacing.Label(current, v.Loc.Language, FramePacing.CurrentHz), options.IndexOf(current), options.Count, next =>
            {
                int value = options[next]; draft["fps"] = value;
                if (!v.Services.Settings.SetFrameRate(value)) { draft["fps"] = v.Model.Settings["fps"]; PanelUi.SaveFailure(v); }
            });
        }

        /// <summary>
        /// ‹ value › selector (G101-Menu step rows): arrows at 30 % and inert at the ends, ←/→ on the keyboard, a fixed
        /// one-line description. Follows the window to another monitor while the panel is open.
        /// </summary>
        private void Stepper(MenuView v, VisualElement list, string key, string title, string description, string label, int index, int count, Action<int> pick)
        {
            void Go(int step) { int next = index + step; if (next < 0 || next >= count) return; pick(next); v.Refresh(); }
            // isStep: width 236, height 44, radius 14, dp; ‹ › 38 px wide, 700 20px heading in mt (hover #F4F1E8), .3 at the
            // ends; the value 700 15px, centred, ellipsised.
            int id = segments++;
            var group = v.Row(); group.name = "setting-" + key; group.style.width = 236; group.style.height = 44; group.style.flexShrink = 0; group.style.alignItems = Align.Center; group.focusable = true; v.Decorate(group, "dp", 14);
            Button Arrow(string glyph, string name, int step)
            {
                bool enabled = index + step >= 0 && index + step < count;
                var arrow = PanelUi.CustomButton(v, glyph, () => Go(step), "g-step-arrow"); arrow.name = name;
                arrow.style.width = 38; arrow.style.minWidth = 38; arrow.style.height = Length.Percent(100); arrow.style.paddingLeft = arrow.style.paddingRight = arrow.style.paddingTop = arrow.style.paddingBottom = 0;
                arrow.style.fontSize = 20; arrow.style.color = v.C("mt"); arrow.EnableInClassList("at-end", !enabled); arrow.style.opacity = enabled ? 1 : .3f;
                arrow.RegisterCallback<PointerEnterEvent>(_ => arrow.style.color = v.C("#F4F1E8")); arrow.RegisterCallback<PointerLeaveEvent>(_ => arrow.style.color = v.C("mt"));
                return arrow;
            }
            var value = v.Text(label, 15, true); value.name = "setting-" + key + "-value"; PanelUi.Grow(value);
            value.style.unityTextAlign = TextAnchor.MiddleCenter; value.style.whiteSpace = WhiteSpace.NoWrap; value.style.overflow = Overflow.Hidden; value.style.textOverflow = TextOverflow.Ellipsis;
            group.Add(Arrow("‹", "setting-" + key + "-prev", -1)); group.Add(value); group.Add(Arrow("›", "setting-" + key + "-next", 1));
            group.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.LeftArrow) { Go(-1); evt.StopPropagation(); }
                else if (evt.keyCode == KeyCode.RightArrow) { Go(1); evt.StopPropagation(); }
            });
            var row = SettingRow(v, list, title, description, group, "g", id);
            // One line, fixed height: the row never jumps when the text changes (spec/19).
            var desc = row.Query<Label>().AtIndex(1);
            if (desc != null) { desc.style.whiteSpace = WhiteSpace.NoWrap; desc.style.overflow = Overflow.Hidden; desc.style.textOverflow = TextOverflow.Ellipsis; }
            Action changed = () => v.Refresh();
            row.RegisterCallback<AttachToPanelEvent>(_ => FramePacing.DisplayChanged += changed);
            row.RegisterCallback<DetachFromPanelEvent>(_ => FramePacing.DisplayChanged -= changed);
        }

        private void Languages(MenuView v, VisualElement list)
        {
            // grid 1fr 1fr, gap 14, padding 6px 0.
            var row = v.Row(); row.style.alignItems = Align.Stretch; row.style.paddingTop = row.style.paddingBottom = 6; int n = 0;
            foreach (var id in new[] { "tr", "en" })
            {
                var lang = id; int k = n++;
                var card = PanelUi.CustomButton(v, "", () =>
                {
                    draft["lang"] = lang;
                    if (!v.Services.Settings.SetLanguage(lang)) { draft["lang"] = v.Model.Settings["lang"]; PanelUi.SaveFailure(v); }
                }, "g-language-card");
                card.name = "menu.l.pick[" + k + "]"; card.EnableInClassList("selected", (string)draft["lang"] == lang);
                card.style.flexDirection = FlexDirection.Row; card.style.alignItems = Align.Center;
                // Reference: 44px code tile (radius 12, 700 16px heading in a1), gap 14, 700 18px/1.2 name over 700 12px sub, 24px radio with a 12px dot.
                var code = v.Box(); code.style.width = code.style.height = 44; code.style.flexShrink = 0; code.style.alignItems = Align.Center; code.style.justifyContent = Justify.Center; code.style.marginRight = 14; v.Decorate(code, "dp", 12);
                var codeText = v.Text(lang.ToUpperInvariant(), 16, true, "a1"); codeText.name = "menu.l.code[" + k + "]"; code.Add(codeText); card.Add(code);
                var nameLabel = v.Text(lang == "tr" ? "Türkçe" : "English", 18, true); nameLabel.name = "menu.l.name[" + k + "]"; nameLabel.style.height = 21.6f;
                var subLabel = v.Text(lang == "tr" ? v.L("Türkiye", "Turkey") : v.L("Uluslararası", "International"), 12, false, "mt"); subLabel.name = "menu.l.sub[" + k + "]";
                var labels = v.Column(nameLabel, subLabel); PanelUi.Grow(labels); labels.style.marginRight = 14; card.Add(labels);
                var selected = (string)draft["lang"] == lang;
                var radio = PanelUi.Circle(v, 24, "transparent");
                radio.style.borderTopWidth = radio.style.borderBottomWidth = radio.style.borderLeftWidth = radio.style.borderRightWidth = 2;
                radio.style.borderTopColor = radio.style.borderBottomColor = radio.style.borderLeftColor = radio.style.borderRightColor = v.C(selected ? "a1" : "bd");
                if (selected) radio.Add(PanelUi.Circle(v, 12, "a1"));
                card.Add(radio);
                if (lang == "tr") card.style.marginRight = 14; row.Add(card);
            }
            list.Add(row);
        }

        private void Account(MenuView v, VisualElement list)
        {
            Toggle(v, list, "friendReq", v.L("Arkadaşlık istekleri", "Friend requests"), v.L("Yeni istekler için bildirim al", "Get notified about new requests"));
            Toggle(v, list, "invites", v.L("Masa davetleri", "Table invites"), v.L("Arkadaşların seni masaya çağırabilsin", "Let friends invite you to their table"));
            var accountName = v.Text((string)v.Model.Profile["name"], 20, true); accountName.name = "menu.me.name#2"; accountName.style.height = 24;
            var playerId = v.Text(v.L("Oyuncu ID · 4821 9930", "Player ID · 4821 9930"), 13, false, "mt"); playerId.name = "menu.t.playerId";
            var details = v.Column(accountName, playerId); PanelUi.Grow(details);
            // v4: the 62px framed avatar of the top bar (frameOf: insetS, a 3px ring in the frame's gap colour), gap 16.
            var avatar = v.Avatar(62); avatar.style.marginRight = 16; avatar.Q<Label>("avatar-initials").name = "menu.me.ini#2";
            var edit = PanelUi.CssButton(v, v.L("Profili düzenle", "Edit profile"), () => v.OpenDialog("profile"), "secondary bright-120", 9, 16, 14, 12); edit.name = "menu.openProfile";
            var profile = v.Row(avatar, details, edit); profile.AddToClassList("g-panel-card"); profile.style.borderTopLeftRadius = profile.style.borderTopRightRadius = profile.style.borderBottomLeftRadius = profile.style.borderBottomRightRadius = 20; profile.style.marginTop = 6; profile.style.marginBottom = 14; list.Add(profile);
            var google = v.Box(); google.style.width = google.style.height = 36; google.style.flexShrink = 0; google.style.alignItems = Align.Center; google.style.justifyContent = Justify.Center; google.style.marginRight = 14; v.Decorate(google, "#fff", 10); google.Add(v.Text("G", 18, true, "#4285F4"));
            var email = v.Column(MenuView.CssLine(v.Text("Google", 16, true), 19.2f), MenuView.CssLine(v.Text("dogukan•••@gmail.com", 12, false, "mt"))); PanelUi.Grow(email); // 700 16px/1.2 over 700 12px
            var linkedLabel = v.Text(v.L("Bağlı", "Linked"), 12, false, "#8EF0B0"); linkedLabel.style.unityFont = v.Font("Baloo2-800");
            var linkedDot = PanelUi.Circle(v, 8, "#3CCB6E"); linkedDot.style.marginRight = 6;
            var linked = v.Row(google, email, v.Row(linkedDot, linkedLabel)); linked.AddToClassList("g-panel-card"); PanelUi.Pad(linked, 14, 18); linked.style.borderTopLeftRadius = linked.style.borderTopRightRadius = linked.style.borderBottomLeftRadius = linked.style.borderBottomRightRadius = 18; linked.style.marginBottom = 14; list.Add(linked);
            var logout = PanelUi.CssButton(v, v.L("Çıkış yap", "Log out"), () => v.OpenDialog("logout"), "danger bright-120", 12, 20, 15); logout.name = "menu.askLogout";
            var delete = PanelUi.CustomButton(v, v.L("Hesabı sil", "Delete account"), () => v.OpenDialog("delete"), ""); delete.name = "menu.askDelete"; delete.style.color = v.C("mt"); delete.style.fontSize = 14; delete.style.unityFont = v.Text("", 14).style.unityFont;
            delete.style.paddingTop = delete.style.paddingBottom = 12; delete.style.paddingLeft = delete.style.paddingRight = 4;
            delete.RegisterCallback<PointerEnterEvent>(_ => delete.style.color = v.C("#FF8A94")); delete.RegisterCallback<PointerLeaveEvent>(_ => delete.style.color = v.C("mt"));
            // display:flex without align-items: both stretch to the taller "Hesabı sil", the labels keep their top padding.
            logout.style.justifyContent = Justify.FlexStart; var actions = v.Row(logout, delete); actions.style.alignItems = Align.Stretch; PanelUi.Gap(actions, 12); actions.style.marginTop = 4; list.Add(actions);
        }
    }
}
