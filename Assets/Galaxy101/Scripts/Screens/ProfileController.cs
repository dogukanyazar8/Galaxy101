using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    public sealed class ProfileController
    {
        public string[] Tabs(MenuView v) => new[] { v.L("Genel", "Overview"), v.L("Rozetler", "Badges"), v.L("Maç geçmişi", "Match history") };

        public VisualElement Build(MenuView v)
        {
            var root = PanelUi.Shell(v, "Profile");
            root.style.alignItems = Align.Stretch;
            var stats = v.Services.Profile.GetStats();
            var summary = root.Q<VisualElement>("summary");
            summary.style.width = 290; summary.style.flexShrink = 0; summary.style.marginRight = 18; summary.style.alignSelf = Align.Stretch;
            summary.style.alignItems = Align.Center; PanelUi.Pad(summary, 22, 18);
            summary.style.paddingBottom = 18;
            v.Decorate(summary, "radial-gradient(ellipse at 50% 20%,var(--pnH),var(--sd) 70%)", 22);
            summary.Add(v.Avatar(124, null, true));
            var name = v.Text((string)v.Model.Profile["name"], 24, true); name.style.marginTop = 18; summary.Add(name);
            var status = (string)v.Model.Profile["status"];
            var statusLabel = PanelUi.Wrap(v, string.IsNullOrEmpty(status) ? v.L("Henüz durum mesajı yok", "No status message yet") : status, 13);
            statusLabel.style.unityTextAlign = TextAnchor.MiddleCenter; statusLabel.style.marginTop = 10; statusLabel.style.minHeight = 18; summary.Add(statusLabel);
            var xp = v.Row(v.Text(v.L("Seviye 25'e", "To level 25"), 12, false, "mt"), PanelUi.Spacer(v), v.Text(v.N((int)stats["xp"]) + " / " + v.N((int)stats["nextXp"]) + " XP", 12, false, "mt")); xp.style.alignSelf = Align.Stretch; xp.style.marginTop = 14; summary.Add(xp);
            var progress = v.Progress((float)stats["xp"] / (float)stats["nextXp"]); progress.style.height = 10; progress.style.alignSelf = Align.Stretch; progress.style.marginTop = 6; summary.Add(progress);
            // Reference: dashed strip, padding 10px 12px, radius 14, gap 10; code 700 16px, "Kopyala" 700 13px heading in ic.
            var code = PanelUi.CodeStrip(v, 12, 16, 14); PanelUi.Pad(code, 10, 12); PanelUi.Gap(code, 10); code.style.alignSelf = Align.Stretch; code.style.marginTop = 14; code.Add(PanelUi.Spacer(v));
            Button copy = null;
            copy = PanelUi.CustomButton(v, v.L("Kopyala", "Copy"), () =>
            {
                v.Copy("DY-4821"); copy.text = v.L("Kopyalandı", "Copied");
                copy.schedule.Execute(() => copy.text = v.L("Kopyala", "Copy")).StartingIn(1500);
            }, "");
            copy.style.paddingLeft = copy.style.paddingRight = copy.style.paddingTop = copy.style.paddingBottom = 0; copy.style.fontSize = 13; copy.style.color = v.C("ic");
            copy.RegisterCallback<PointerEnterEvent>(_ => copy.style.color = UnityEngine.Color.white); copy.RegisterCallback<PointerLeaveEvent>(_ => copy.style.color = v.C("ic"));
            code.Add(copy); summary.Add(code);
            summary.Add(PanelUi.Spacer(v));
            var edit = PanelUi.Action(v, v.L("Profili düzenle", "Edit profile"), () => v.OpenDialog("profile")); PanelUi.Grow(edit);
            edit.style.flexDirection = FlexDirection.Row; PanelUi.Pad(edit, 12, 0);
            var pencil = v.Icon("edit", 16, "aT"); pencil.style.marginRight = 8; edit.Insert(0, pencil);
            var share = v.Button("", () => v.OpenDialog("share"), "secondary"); share.name = "share-profile"; share.tooltip = v.L("Profili paylaş", "Share profile"); share.style.width = share.style.height = 46; share.style.minWidth = 46; share.style.paddingLeft = share.style.paddingRight = 0; share.style.marginLeft = 10; share.Add(v.Icon("share", 20));
            var actions = v.Row(edit, share); actions.style.alignSelf = Align.Stretch; summary.Add(actions);
            var detail = root.Q<ScrollView>("detail");
            detail.mode = ScrollViewMode.Vertical;
            detail.verticalScrollerVisibility = ScrollerVisibility.AlwaysVisible;
            detail.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            detail.style.paddingRight = 4; detail.style.minWidth = 0;
            switch (v.Tab("me"))
            {
                case 0: Overview(v, detail, stats); break;
                case 1: Badges(v, detail); break;
                case 2: History(v, detail); break;
            }
            return root;
        }

        private static void Overview(MenuView v, VisualElement parent, JObject stats)
        {
            var row = v.Row(); row.style.alignItems = Align.Stretch;
            row.Add(Stat(v, v.L("OYNANAN EL", "HANDS PLAYED"), v.N((int)stats["hands"]), v.L("527 kazanıldı", "527 won"), "#F4F1E8"));
            row.Add(Stat(v, v.L("KAZANMA", "WIN RATE"), v.P((int)stats["winRate"]), v.L("Bu ay +%3", "+3% this month"), "#3CCB6E"));
            row.Add(Stat(v, v.L("EN İYİ AÇILIŞ", "BEST OPENING"), v.N((int)stats["bestOpening"]), v.L("puan", "points"), "a1"));
            row.Add(Stat(v, v.L("OKEY BİTİŞ", "OKEY FINISH"), v.N((int)stats["okeyFinish"]), v.L("katlanan el", "doubled hands"), "#F4F1E8"));
            PanelUi.Gap(row, 10); parent.Add(row);
            var middle = v.Row(); middle.style.marginTop = 14; middle.style.alignItems = Align.Stretch;
            var rank = v.Box("g-panel-card"); rank.name = "profile-rank"; PanelUi.Pad(rank, 16, 18); rank.style.flexGrow = 1.1f; rank.style.flexBasis = 0; rank.style.marginRight = 10;
            var diamond = v.Box(); diamond.style.width = diamond.style.height = 60; diamond.style.flexShrink = 0; diamond.style.alignItems = Align.Center; diamond.style.justifyContent = Justify.Center;
            var gem = v.Box(); gem.style.width = gem.style.height = 52; gem.style.rotate = new Rotate(45); gem.style.alignItems = Align.Center; gem.style.justifyContent = Justify.Center; v.Decorate(gem, "linear-gradient(135deg,#FFE08A,#F5A623)", 12);
            GalaxyFilters.Shadow(gem, 0, 0, 11, v.C("rgba(255,201,74,.5)"));
            var numeral = v.Text("II", 20, true, "#3A1F00"); numeral.style.rotate = new Rotate(-45); gem.Add(numeral); diamond.Add(gem);
            var description = v.Column(PanelUi.Caption(v, v.L("SEZON 3 · DERECELİ", "SEASON 3 · RANKED"), 10, 1.2f), v.Text(v.L("Altın II", "Gold II"), 22, true, "#FFC94A")); description.style.marginLeft = 14; PanelUi.Grow(description);
            var best = v.Column(PanelUi.Caption(v, v.L("SEZON EN İYİ", "SEASON BEST"), 10, 1.2f), v.Text(v.L("Altın I", "Gold I"), 15, true)); best.style.alignItems = Align.FlexEnd;
            rank.Add(v.Row(diamond, description, best));
            var lp = v.Progress((float)stats["lp"] / (float)stats["nextLp"], "linear-gradient(90deg,#F5A623,#FFE08A)"); lp.style.marginTop = 12; lp.style.height = 10; rank.Add(lp);
            var rankFooter = v.Row(v.Text("720 / 1000 LP", 12, false, "mt"), PanelUi.Spacer(v), v.Text(v.L("Altın I'e 280 LP", "280 LP to Gold I"), 12, false, "mt")); rankFooter.style.marginTop = 12; rank.Add(rankFooter); middle.Add(rank);
            var win = v.Box("g-panel-card"); win.name = "profile-mode-win-rates"; PanelUi.Pad(win, 14, 18); win.style.flexGrow = 1; win.style.flexBasis = 0; win.Add(PanelUi.Caption(v, v.L("MODLARA GÖRE KAZANMA", "WIN RATE BY MODE"), 10, 1.2f));
            var rates = (JArray)stats["modeWinRates"];
            var modes = (JArray)v.Model.Content["modes"];
            for (var i = 0; i < 4; i++)
            {
                var mode = (JObject)modes[i];
                var name = v.Text(v.L((string)mode["tr"], (string)mode["en"]), 13, true); name.style.width = 86; name.style.height = 16; name.style.unityTextAlign = TextAnchor.MiddleLeft; name.style.marginRight = 10;
                var bar = v.Progress((float)rates[i] / 100, (string)mode["bg"]); PanelUi.Grow(bar); bar.style.height = 8; bar.style.marginRight = 10;
                var value = v.Text(v.P((int)rates[i]), 13, true); value.style.width = 40; value.style.height = 16; value.style.unityTextAlign = TextAnchor.MiddleRight;
                var line = v.Row(name, bar, value); line.style.marginTop = 9; win.Add(line);
            }
            middle.Add(win); parent.Add(middle);
            var frames = v.Box("g-panel-card"); frames.name = "profile-frames"; PanelUi.Pad(frames, 14, 18); frames.style.marginTop = 14;
            var framesLabel = v.Text(v.L("ÇERÇEVELER", "FRAMES"), 10, false, "mt"); framesLabel.style.unityFont = v.Font("Baloo2-800"); MenuView.Tracking(framesLabel, 1.2f, 10);
            frames.Add(v.Row(framesLabel, PanelUi.Spacer(v), v.Text(v.L("Takmak için dokun", "Tap to equip"), 12, false, "mt")));
            var frameRow = v.Row(); frameRow.style.marginTop = 10; frameRow.style.alignItems = Align.Stretch;
            for (var i = 0; i < 3; i++)
            {
                var frame = i; var locked = i == 2; var equipped = (int)v.Model.Profile["frame"] == i;
                var button = v.Button("", () =>
                {
                    if (!locked && !v.Services.Profile.Save((string)v.Model.Profile["name"], (string)v.Model.Profile["color"], (string)v.Model.Profile["status"], frame)) PanelUi.SaveFailure(v);
                }, "secondary");
                button.name = "equip-frame-" + i; button.style.flexGrow = 1; button.style.flexBasis = 0; button.style.flexDirection = FlexDirection.Row; button.style.alignItems = Align.Center; button.style.justifyContent = Justify.FlexStart;
                button.style.paddingTop = button.style.paddingBottom = 10; button.style.paddingLeft = button.style.paddingRight = 12;
                v.Decorate(button, equipped ? "pnH" : "dp", 14);
                if (equipped)
                {
                    var ring = v.Box(); ring.pickingMode = PickingMode.Ignore; ring.style.position = Position.Absolute;
                    ring.style.left = ring.style.right = ring.style.top = ring.style.bottom = 0;
                    ring.style.borderTopWidth = ring.style.borderRightWidth = ring.style.borderBottomWidth = ring.style.borderLeftWidth = 2;
                    ring.style.borderTopColor = ring.style.borderRightColor = ring.style.borderBottomColor = ring.style.borderLeftColor = v.C("a1");
                    ring.style.borderTopLeftRadius = ring.style.borderTopRightRadius = ring.style.borderBottomLeftRadius = ring.style.borderBottomRightRadius = 14; button.Add(ring);
                }
                var preview = (JObject)v.Model.Profile.DeepClone(); preview["frame"] = frame;
                var avatar = v.Avatar(40, preview); foreach (var label in avatar.Query<Label>().ToList()) label.style.display = DisplayStyle.None; avatar.style.marginRight = 12; button.Add(avatar);
                var frameName = v.Text(v.L(new[] { "Klasik", "Altın", "Galaksi" }[i], new[] { "Classic", "Gold", "Galaxy" }[i]), 15, true); frameName.style.unityTextAlign = TextAnchor.MiddleLeft;
                var frameState = PanelUi.Wrap(v, locked ? v.L("Seviye 30'da açılır", "Unlocks at level 30") : equipped ? v.L("Takılı", "Equipped") : v.L("Sahipsin", "Owned"), 11, equipped ? "#8EF0B0" : "mt"); frameState.style.unityTextAlign = TextAnchor.MiddleLeft;
                var frameText = v.Column(frameName, frameState); frameText.style.alignItems = Align.FlexStart; frameText.style.minWidth = 0; button.Add(frameText);
                if (locked) { button.SetEnabled(false); button.style.opacity = .55f; }
                if (i < 2) button.style.marginRight = 10; frameRow.Add(button);
            }
            frames.Add(frameRow); parent.Add(frames);
        }

        private static VisualElement Stat(MenuView v, string title, string value, string sub, string color)
        {
            var card = v.Box("g-profile-stat");
            card.style.paddingBottom = 12;
            var label = PanelUi.Caption(v, title, 10, 1.2f); card.Add(label);
            var number = v.Text(value, 28, true, color); number.style.height = 30.8f; number.style.marginTop = 4; card.Add(number);
            var description = v.Text(sub, 12, false, "mt"); description.style.marginTop = 4; card.Add(description); return card;
        }

        private static void Badges(MenuView v, VisualElement parent)
        {
            var grid = v.Column(); parent.Add(grid);
            VisualElement badgeRow = null; int index = 0;
            foreach (JObject badge in v.Services.Profile.GetBadges())
            {
                if (index % 3 == 0)
                {
                    badgeRow = v.Row(); badgeRow.style.alignItems = Align.Stretch; badgeRow.style.flexShrink = 0;
                    if (index > 0) badgeRow.style.marginTop = 10;
                    grid.Add(badgeRow);
                }
                var locked = (bool)badge["locked"];
                var card = v.Box("g-profile-badge"); card.style.alignItems = Align.Stretch;
                if (index % 3 < 2) card.style.marginRight = 10;
                if (locked) card.style.opacity = .75f;
                var art = v.Box(); art.style.width = art.style.height = 46; art.style.flexShrink = 0;
                art.style.alignItems = Align.Center; art.style.justifyContent = Justify.Center; art.style.marginRight = 12;
                var color = (string)badge["color"];
                v.Decorate(art, locked ? "dp" : "linear-gradient(160deg," + color + "," + color + "AA)", 14, locked ? 0 : 3, "rgba(0,0,0,.3)");
                if (locked)
                {
                    var ring = v.Box(); ring.pickingMode = PickingMode.Ignore; ring.style.position = Position.Absolute;
                    ring.style.left = ring.style.right = ring.style.top = ring.style.bottom = 0;
                    ring.style.borderTopWidth = ring.style.borderRightWidth = ring.style.borderBottomWidth = ring.style.borderLeftWidth = 1;
                    ring.style.borderTopColor = ring.style.borderRightColor = ring.style.borderBottomColor = ring.style.borderLeftColor = v.C("rgba(255,255,255,.08)");
                    ring.style.borderTopLeftRadius = ring.style.borderTopRightRadius = ring.style.borderBottomLeftRadius = ring.style.borderBottomRightRadius = 14;
                    art.Add(ring);
                }
                else { var glow = v.C(color); glow.a = .4f; GalaxyFilters.Shadow(art, 0, 0, 8, glow); }
                art.Add(v.Text((string)badge["glyph"], 18, true, "#fff"));
                var description = PanelUi.Wrap(v, v.L((string)badge["desc_tr"], (string)badge["desc_en"]), 12); description.style.marginTop = 2;
                var text = v.Column(PanelUi.Wrap(v, v.L((string)badge["tr"], (string)badge["en"]), 15, "#F4F1E8", true), description); PanelUi.Grow(text);
                if (locked)
                {
                    var progress = v.Progress((float)badge["cur"] / (float)badge["max"], "mt"); progress.style.height = 6; PanelUi.Grow(progress); progress.style.marginRight = 8;
                    var fill = progress.Q<VisualElement>(className: "progress-fill");
                    fill.style.borderTopLeftRadius = fill.style.borderTopRightRadius = fill.style.borderBottomLeftRadius = fill.style.borderBottomRightRadius = 0;
                    var count = v.Text(v.N((int)badge["cur"]) + "/" + v.N((int)badge["max"]), 11, false, "mt"); count.style.unityFont = v.Font("Baloo2-800");
                    var row = v.Row(progress, count); row.style.marginTop = 6; text.Add(row);
                }
                card.Add(v.Row(art, text)); badgeRow.Add(card); index++;
            }
        }

        private static void History(MenuView v, VisualElement parent)
        {
            var header = v.Row(v.Box(), PanelUi.Caption(v, v.L("MOD", "MODE"), 10, 1.2f), PanelUi.Caption(v, v.L("SIRA", "PLACE"), 10, 1.2f), PanelUi.Caption(v, v.L("ÇİP", "CHIPS"), 10, 1.2f), PanelUi.Caption(v, v.L("ZAMAN", "WHEN"), 10, 1.2f)); HistoryColumns(header); header.style.marginBottom = 12; parent.Add(header);
            foreach (JObject match in v.Services.Profile.GetHistory())
            {
                var mode = (JObject)v.Model.Content["modes"][(int)match["mode"]];
                var art = PanelUi.Badge(v, (string)mode["glyph"], (string)mode["bg"], "#fff", 14); art.style.width = art.style.height = 30; art.style.justifyContent = Justify.Center;
                PanelUi.Pad(art, 0, 0); var artColumn = v.Box(); artColumn.Add(art);
                var details = v.Column(v.Text(v.L((string)mode["tr"], (string)mode["en"]), 15, true), v.Text((string)match["lp"] ?? v.L("101 · 5K masa", "101 · 5K table"), 12, false, "mt"));
                var place = (int)match["place"];
                var rank = v.Box(); rank.Add(PanelUi.Badge(v, v.L(place + ".", "#" + place), place == 1 ? "rgba(255,201,74,.18)" : "dp", place == 1 ? "#FFC94A" : "mt", 11)); rank.style.alignItems = Align.FlexStart;
                var delta = (int)match["delta"];
                var minutes = (int)match["minutes"];
                var ago = minutes < 60 ? v.L(minutes + " dk önce", minutes + " min ago") : minutes < 1440 ? v.L(minutes / 60 + " sa önce", minutes / 60 + " h ago") : v.L("Dün", "Yesterday");
                var row = v.Row(artColumn, details, rank, v.Text(delta == 0 ? "—" : (delta > 0 ? "+" : "−") + v.N(System.Math.Abs(delta)), 15, true, delta > 0 ? "#8EF0B0" : delta < 0 ? "#FF8A94" : "mt"), v.Text(ago, 12, false, "mt"));
                row.AddToClassList("g-friend-row"); row.style.paddingTop = row.style.paddingBottom = 10; row.style.minHeight = 58; row.style.borderTopRightRadius = row.style.borderBottomRightRadius = 0; HistoryColumns(row); parent.Add(row);
            }
        }

        private static void HistoryColumns(VisualElement row)
        {
            row[0].style.width = 44; row[0].style.flexShrink = 0;
            PanelUi.Grow(row[1]);
            row[2].style.width = 110; row[2].style.flexShrink = 0;
            row[3].style.width = 110; row[3].style.flexShrink = 0; row[3].style.unityTextAlign = TextAnchor.MiddleRight;
            row[4].style.width = 90; row[4].style.flexShrink = 0; row[4].style.unityTextAlign = TextAnchor.MiddleRight;
        }
    }
}
