using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    public sealed class SettingsController
    {
        private JObject draft;
        private MenuView owner;

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
            var titleText = v.Text(v.L("Ayarlar", "Settings"), 26, true); titleText.style.height = 28.6f; titleText.style.unityTextAlign = TextAnchor.MiddleLeft; titleText.style.whiteSpace = WhiteSpace.NoWrap;
            var title = v.Row(v.Icon("settings", 26, "a1"), titleText); PanelUi.Gap(title, 10); title.AddToClassList("g-settings-title"); sidebar.Add(title);
            var titles = new[] { v.L("Oyun", "Game"), v.L("Ses", "Sound"), v.L("Görüntü", "Display"), v.L("Dil", "Language"), v.L("Hesap", "Account") };
            for (var i = 0; i < titles.Length; i++)
            {
                var index = i;
                var button = PanelUi.CustomButton(v, "", () => v.SetTab("settings", index), "g-settings-nav");
                button.style.flexDirection = FlexDirection.Row; button.style.justifyContent = Justify.FlexStart;
                // The active bar is an inset box-shadow in the reference, so it must not move the label like a border would.
                if (i == tab)
                {
                    var bar = v.Box("g-settings-nav-bar"); bar.pickingMode = PickingMode.Ignore; v.Decorate(bar, "a1", 14);
                    var face = v.Box("g-settings-nav-face"); face.pickingMode = PickingMode.Ignore; v.Decorate(face, "pnH", 14); bar.Add(face); button.Add(bar);
                }
                var diamond = v.Box("g-settings-nav-dot"); diamond.style.width = diamond.style.height = 8; diamond.style.rotate = new Rotate(45); diamond.style.marginRight = 12;
                v.Decorate(diamond, i == tab ? "a1" : "bd", 3); button.Add(diamond);
                var label = v.Text(titles[i], 17, true); label.style.color = StyleKeyword.Null; label.style.height = 20.4f; label.style.unityTextAlign = TextAnchor.MiddleLeft; label.style.whiteSpace = WhiteSpace.NoWrap; button.Add(label);
                button.EnableInClassList("selected", i == tab); button.name = "settings-tab-" + i; sidebar.Add(button);
            }
            sidebar.Add(PanelUi.Spacer(v));
            // Reference: one block, padding 0 12px, 700 11px body font, muted at .7 opacity.
            var credits = v.Column(v.Text("Galaxy 101 · v0.2.0", 11, false, "mt"), v.Text("© CDX Games", 11, false, "mt")); credits.AddToClassList("g-settings-credits"); sidebar.Add(credits);
            var content = root.Q<VisualElement>("content");
            var subtitles = new[]
            {
                v.L("Taş hareketleri ve masa davranışı", "Tile movement and table behavior"),
                v.L("Müzik, efektler ve bildirim sesleri", "Music, effects and notification sounds"),
                v.L("Tema, çözünürlük ve görsel efektler", "Theme, resolution and visual effects"),
                v.L("Uygulamanın dili", "App language"),
                v.L("Profil, bağlı hesaplar ve bildirimler", "Profile, linked accounts and notifications")
            };
            var heading = v.Row(v.Column(v.Text(titles[tab], 24, true), v.Text(subtitles[tab], 13, false, "mt")), PanelUi.Spacer(v));
            heading.AddToClassList("g-settings-heading");
            var close = v.Button("", () => Cancel(v), "secondary"); close.name = "settings-close";
            close.style.width = close.style.height = 42; close.style.minWidth = 42; close.style.paddingLeft = close.style.paddingRight = 0; close.Add(v.Icon("close", 18)); heading.Add(close); content.Add(heading);
            var scroll = v.Scroll(); scroll.AddToClassList("g-settings-list"); content.Add(scroll);
            switch (tab)
            {
                case 0:
                    Toggle(v, scroll, "autoSort", v.L("Otomatik sırala", "Auto-sort"), v.L("Yeni çekilen taş ıstakada uygun yere yerleşsin", "Drawn tiles go to the right spot on your rack"));
                    Toggle(v, scroll, "highlight", v.L("Perleri vurgula", "Highlight sets"), v.L("Geçerli perlerin çevresi yeşil yansın", "Valid sets glow green"));
                    Toggle(v, scroll, "dbl", v.L("Çift tıkla taş at", "Double-click to discard"), v.L("Sürüklemeden hızlı atış", "Discard quickly without dragging"));
                    Toggle(v, scroll, "chat", v.L("Masa sohbeti", "Table chat"), v.L("Masa içi mesaj ve emojiler görünsün", "Show messages and emojis at the table"));
                    Slider(v, scroll, "warn", v.L("Tur süresi uyarısı", "Turn timer warning"), v.L("Süre bitmeden kaç saniye önce uyarılsın", "How many seconds before your time runs out"), 1, 10, v.L(" sn", " s"));
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
                    Resolution(v, scroll);
                    Segment(v, scroll, "quality", v.L("Efekt kalitesi", "Effect quality"), v.L("Düşük ayar eski bilgisayarlarda akıcılığı artırır", "Lower settings run smoother on older computers"), new[] { "Düşük", "Orta", "Yüksek" });
                    Planets(v, scroll);
                    break;
                case 3: Languages(v, scroll); break;
                case 4: Account(v, scroll); break;
            }
            var footer = v.Row(); footer.AddToClassList("g-settings-footer");
            var reset = PanelUi.CustomButton(v, v.L("Varsayılana dön", "Reset to default"), () =>
            {
                draft = v.Services.Settings.ResetToDefaults();
                if (!v.Services.Settings.SetLanguage((string)draft["lang"])) { draft["lang"] = v.Model.Settings["lang"]; PanelUi.SaveFailure(v); }
                if (!v.Services.Settings.SetResolution((int)draft["resolution"])) { draft["resolution"] = v.Model.Settings["resolution"]; PanelUi.SaveFailure(v); }
                v.PreviewSettings(draft); v.Refresh();
            }, "");
            reset.name = "settings-reset"; reset.style.backgroundColor = UnityEngine.Color.clear; reset.style.color = v.C("mt"); reset.style.fontSize = 14;
            reset.style.paddingLeft = reset.style.paddingRight = 0; reset.style.unityFont = v.Text("", 14).style.unityFont;
            footer.Add(reset); footer.Add(PanelUi.Spacer(v));
            footer.Add(PanelUi.CssButton(v, v.L("Vazgeç", "Cancel"), () => Cancel(v), "secondary", 12, 22, 16));
            var save = PanelUi.CssButton(v, v.L("Kaydet", "Save"), () =>
            {
                if (!v.Services.Settings.Save(draft)) { PanelUi.SaveFailure(v); return; }
                draft = null; v.CloseTop();
            }, "primary", 12, 28, 16);
            save.name = "settings-save"; save.style.marginLeft = 12; footer.Add(save); content.Add(footer);
            return root;
        }

        private static VisualElement SettingRow(MenuView v, VisualElement list, string title, string description, VisualElement control)
        {
            var labels = v.Column(v.Text(title, 17, true), PanelUi.Wrap(v, description, 13)); PanelUi.Grow(labels); labels.style.marginRight = 16;
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
            button.Add(v.Box("g-toggle-knob")); SettingRow(v, list, title, description, button);
        }

        private void Slider(MenuView v, VisualElement list, string key, string title, string description, int min, int max, string unit)
        {
            var value = (int)draft[key]; var label = v.Text(value + unit, 15, true); label.AddToClassList("g-slider-value");
            PanelUi.Pad(label, 6, 0);
            label.style.color = v.C(value == min ? "mt" : "#F4F1E8");
            var slider = new GalaxySlider(min, max, value, v); slider.name = "setting-" + key;
            slider.style.marginRight = 18;
            slider.ValueChanged += x => { draft[key] = x; label.text = x + unit; label.style.color = v.C(x == min ? "mt" : "#F4F1E8"); };
            var row = v.Row(slider, label); row.style.flexShrink = 0; row.style.marginLeft = 2;
            SettingRow(v, list, title, description, row);
        }

        private void Segment(MenuView v, VisualElement list, string key, string title, string description, string[] values)
        {
            var group = v.Row(); group.AddToClassList("g-segment");
            foreach (var value in values)
            {
                var option = value;
                var button = PanelUi.CustomButton(v, v.L(option), () => { draft[key] = option; v.Refresh(); }, "g-segment-button");
                button.EnableInClassList("selected", (string)draft[key] == option);
                if ((string)draft[key] == option) v.Decorate(button, "linear-gradient(var(--a1),var(--a2))", 10, 3, "aS");
                group.Add(button);
            }
            PanelUi.Gap(group, 4);
            SettingRow(v, list, title, description, group);
        }

        private void Resolution(MenuView v, VisualElement list)
        {
            var group = v.Row(); group.AddToClassList("g-segment"); var values = new[] { 720, 1080, 1440 };
            for (var i = 0; i < values.Length; i++)
            {
                var height = values[i]; var button = PanelUi.CustomButton(v, new[] { "720p", "1080p", "2K" }[i], () =>
                {
                    draft["resolution"] = height;
                    if (!v.Services.Settings.SetResolution(height)) { draft["resolution"] = v.Model.Settings["resolution"]; PanelUi.SaveFailure(v); }
                }, "g-segment-button");
                button.EnableInClassList("selected", (int)draft["resolution"] == height);
                if ((int)draft["resolution"] == height) v.Decorate(button, "linear-gradient(var(--a1),var(--a2))", 10, 3, "aS");
                group.Add(button);
            }
            PanelUi.Gap(group, 4);
            SettingRow(v, list, v.L("Çözünürlük", "Resolution"), v.L("Ekran boyutu", "Screen size"), group);
        }

        private void Planets(MenuView v, VisualElement list)
        {
            var row = v.Row(); row.style.flexShrink = 0;
            foreach (var property in v.Model.Themes.Properties())
            {
                var planet = property.Name; var data = (JObject)property.Value; var orb = (JArray)data["pickerOrb"];
                var button = PanelUi.CustomButton(v, "", () => { if (!v.Services.Settings.SetPlanet(planet)) PanelUi.SaveFailure(v); }, "");
                // Reference: 62px tile, padding 8px 0, gap 5; the orb carries the same rings as the main planet picker.
                var on = planet == v.Model.Planet;
                button.style.width = 62; button.style.minWidth = 62; button.style.height = StyleKeyword.Auto; button.style.paddingTop = button.style.paddingBottom = 8; button.style.paddingLeft = button.style.paddingRight = 0; button.style.marginRight = 8;
                button.style.alignItems = Align.Center; button.style.justifyContent = Justify.FlexStart; v.Decorate(button, on ? "pnH" : "transparent", 14);
                button.Add(v.PlanetOrb(planet, 30, on));
                var name = v.Text(v.L((string)data["name_tr"], (string)data["name_en"]), 12, false, on ? "#F4F1E8" : "mt"); name.style.marginTop = 5; button.Add(name); row.Add(button);
            }
            row[row.childCount - 1].style.marginRight = 0;
            SettingRow(v, list, v.L("Gezegen teması", "Planet theme"), v.L("Renkler, logo ve arka plan seçtiğin gezegene göre değişir", "Colors, logo and background follow the planet you pick"), row);
        }

        private void Languages(MenuView v, VisualElement list)
        {
            var row = v.Row(); row.style.alignItems = Align.Stretch;
            foreach (var id in new[] { "tr", "en" })
            {
                var lang = id;
                var card = PanelUi.CustomButton(v, "", () =>
                {
                    draft["lang"] = lang;
                    if (!v.Services.Settings.SetLanguage(lang)) { draft["lang"] = v.Model.Settings["lang"]; PanelUi.SaveFailure(v); }
                }, "g-language-card");
                card.name = "language-" + lang; card.EnableInClassList("selected", (string)draft["lang"] == lang);
                card.style.flexDirection = FlexDirection.Row; card.style.alignItems = Align.Center;
                // Reference: 44px code tile (radius 12, 700 16px heading in a1), gap 14, 700 18px/1.2 name over 700 12px sub, 24px radio with a 12px dot.
                var code = v.Box(); code.style.width = code.style.height = 44; code.style.flexShrink = 0; code.style.alignItems = Align.Center; code.style.justifyContent = Justify.Center; code.style.marginRight = 14; v.Decorate(code, "dp", 12);
                code.Add(v.Text(lang.ToUpperInvariant(), 16, true, "a1")); card.Add(code);
                var nameLabel = v.Text(lang == "tr" ? "Türkçe" : "English", 18, true); nameLabel.style.height = 21.6f;
                var labels = v.Column(nameLabel, v.Text(lang == "tr" ? v.L("Türkiye", "Turkey") : v.L("Uluslararası", "International"), 12, false, "mt")); PanelUi.Grow(labels); labels.style.marginRight = 14; card.Add(labels);
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
            var accountName = v.Text((string)v.Model.Profile["name"], 20, true); accountName.style.height = 24;
            var details = v.Column(accountName, v.Text(v.L("Oyuncu ID · 4821 9930", "Player ID · 4821 9930"), 13, false, "mt")); PanelUi.Grow(details);
            // Reference: a plain 58px avatar with a 3px frame-colored border and the frame's box-shadow, gap 16.
            var avatar = v.AccountAvatar(64); avatar.style.marginRight = 16;
            var profile = v.Row(avatar, details, PanelUi.CssButton(v, v.L("Profili düzenle", "Edit profile"), () => v.OpenDialog("profile"), "secondary", 9, 16, 14, 12)); profile.AddToClassList("g-panel-card"); profile.style.borderTopLeftRadius = profile.style.borderTopRightRadius = profile.style.borderBottomLeftRadius = profile.style.borderBottomRightRadius = 20; profile.style.marginTop = 6; profile.style.marginBottom = 14; list.Add(profile);
            var google = v.Box(); google.style.width = google.style.height = 36; google.style.flexShrink = 0; google.style.alignItems = Align.Center; google.style.justifyContent = Justify.Center; google.style.marginRight = 14; v.Decorate(google, "#fff", 10); google.Add(v.Text("G", 18, true, "#4285F4"));
            var email = v.Column(v.Text("Google", 16, true), v.Text("dogukan•••@gmail.com", 12, false, "mt")); PanelUi.Grow(email);
            var linkedLabel = v.Text(v.L("Bağlı", "Linked"), 12, false, "#8EF0B0"); linkedLabel.style.unityFont = v.Font("Baloo2-800");
            var linkedDot = PanelUi.Circle(v, 8, "#3CCB6E"); linkedDot.style.marginRight = 6;
            var linked = v.Row(google, email, v.Row(linkedDot, linkedLabel)); linked.AddToClassList("g-panel-card"); PanelUi.Pad(linked, 14, 18); linked.style.borderTopLeftRadius = linked.style.borderTopRightRadius = linked.style.borderBottomLeftRadius = linked.style.borderBottomRightRadius = 18; linked.style.marginBottom = 14; list.Add(linked);
            var logout = PanelUi.CssButton(v, v.L("Çıkış yap", "Log out"), () => v.OpenDialog("logout"), "danger", 12, 20, 15);
            var delete = PanelUi.CustomButton(v, v.L("Hesabı sil", "Delete account"), () => v.OpenDialog("delete"), ""); delete.style.color = v.C("mt"); delete.style.fontSize = 14; delete.style.unityFont = v.Text("", 14).style.unityFont;
            delete.style.paddingTop = delete.style.paddingBottom = 12; delete.style.paddingLeft = delete.style.paddingRight = 4;
            delete.RegisterCallback<PointerEnterEvent>(_ => delete.style.color = v.C("#FF8A94")); delete.RegisterCallback<PointerLeaveEvent>(_ => delete.style.color = v.C("mt"));
            var actions = v.Row(logout, delete); PanelUi.Gap(actions, 14); actions.style.marginTop = 4; list.Add(actions);
        }
    }
}
