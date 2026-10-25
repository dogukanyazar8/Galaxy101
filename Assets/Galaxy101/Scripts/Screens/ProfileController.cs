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
            var name = v.Text((string)v.Model.Profile["name"], 24, true); name.style.marginTop = 18; name.style.height = 24 * 1.15f; name.style.unityTextAlign = TextAnchor.MiddleCenter; summary.Add(name);
            var status = (string)v.Model.Profile["status"];
            var statusLabel = PanelUi.Wrap(v, string.IsNullOrEmpty(status) ? v.L("Henüz durum mesajı yok", "No status message yet") : status, 13);
            statusLabel.style.unityTextAlign = TextAnchor.MiddleCenter; statusLabel.style.marginTop = 10; statusLabel.style.minHeight = 18; MenuView.CssParagraph(statusLabel, 13 * 1.4f, false); summary.Add(statusLabel);
            // font: 800 12px Baloo 2; the value in tabular digits.
            Label Bold12(string text) { var label = v.Text(text, 12, false, "mt"); label.style.unityFont = v.Font("Baloo2-800"); return MenuView.CssLine(label); }
            int nextLevel = ((int?)stats["level"] ?? v.Model.Level) + 1;
            var xp = v.Row(Bold12(v.L("Seviye " + nextLevel + Dative(nextLevel), "To level " + nextLevel)), PanelUi.Spacer(v), MenuView.TabularDigits(Bold12(v.N((int)stats["xp"]) + " / " + v.N((int)stats["nextXp"]) + " XP"))); xp.style.alignSelf = Align.Stretch; xp.style.marginTop = 14; summary.Add(xp);
            var progress = v.Progress((float)stats["xp"] / (float)stats["nextXp"]); progress.style.height = 10; progress.style.alignSelf = Align.Stretch; progress.style.marginTop = 6; summary.Add(progress);
            // Reference: dashed strip, padding 10px 12px, radius 14, gap 10; code 700 16px, "Kopyala" 700 13px heading in ic.
            var code = PanelUi.CodeStrip(v, 12, 16, 14); PanelUi.Pad(code, 10, 12); PanelUi.Gap(code, 10); code.style.alignSelf = Align.Stretch; code.style.marginTop = 14; code.Add(PanelUi.Spacer(v));
            Button copy = null;
            copy = PanelUi.CustomButton(v, v.L("Kopyala", "Copy"), () =>
            {
                v.Copy(v.Model.FriendCode); copy.text = v.L("Kopyalandı", "Copied");
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
            string won = v.N((int?)stats["wins"] ?? 0);
            row.Add(Stat(v, v.L("OYNANAN EL", "HANDS PLAYED"), v.N((int)stats["hands"]), v.L(won + " kazanıldı", won + " won"), "#F4F1E8"));
            row.Add(Stat(v, v.L("KAZANMA", "WIN RATE"), v.P((int)stats["winRate"]), v.L("tüm modlarda", "across modes"), "#3CCB6E"));
            row.Add(Stat(v, v.L("EN İYİ AÇILIŞ", "BEST OPENING"), v.N((int)stats["bestOpening"]), v.L("puan", "points"), "a1"));
            row.Add(Stat(v, v.L("OKEY BİTİŞ", "OKEY FINISH"), v.N((int)stats["okeyFinish"]), v.L("katlanan el", "doubled hands"), "#F4F1E8"));
            PanelUi.Gap(row, 10); parent.Add(row);
            var middle = v.Row(); middle.style.marginTop = 14; middle.style.alignItems = Align.Stretch;
            var rank = v.Box("g-panel-card"); rank.name = "profile-rank"; PanelUi.Pad(rank, 16, 18); rank.style.flexGrow = 1.1f; rank.style.flexBasis = 0; rank.style.marginRight = 10;
            var diamond = v.Box(); diamond.style.width = diamond.style.height = 60; diamond.style.flexShrink = 0; diamond.style.alignItems = Align.Center; diamond.style.justifyContent = Justify.Center;
            // The account's league (the design's sample: Altın II, best Altın I, 720 / 1000 LP to Altın I).
            bool sample = stats["league"] == null;
            string league = (string)stats["league"] ?? "g2", nextLeague = sample ? "g1" : (string)stats["nextLeague"], bestLeague = (string)stats["bestLeague"] ?? (sample ? "g1" : league);
            var (gemFill, gemGlow, leagueInk) = LeagueTint(league);
            var gem = v.Box(); gem.style.width = gem.style.height = 52; gem.style.rotate = new Rotate(45); gem.style.alignItems = Align.Center; gem.style.justifyContent = Justify.Center; v.Decorate(gem, gemFill, 12);
            GalaxyFilters.Shadow(gem, 0, 0, 11, v.C(gemGlow));
            var numeral = v.Text(Numeral(league), 20, true, "#3A1F00"); numeral.style.rotate = new Rotate(-45); gem.Add(numeral); diamond.Add(gem);
            int seasonNumber = (int?)stats["season"] ?? 3, place = (int?)stats["seasonRank"] ?? 0;
            string shown = LeagueName(v, league) + (league == "gx" && place > 0 ? " · #" + place : "");
            var description = v.Column(PanelUi.Caption(v, v.L("SEZON " + seasonNumber + " · DERECELİ", "SEASON " + seasonNumber + " · RANKED"), 10, 1.2f), MenuView.CssLine(v.Text(shown, 22, true, leagueInk), 22 * 1.15f)); description.style.marginLeft = 14; PanelUi.Grow(description);
            var best = v.Column(PanelUi.Caption(v, v.L("SEZON EN İYİ", "SEASON BEST"), 10, 1.2f), v.Text(LeagueName(v, bestLeague), 15, true)); best.style.alignItems = Align.FlexEnd;
            rank.Add(v.Row(diamond, description, best));
            var lp = v.Progress((float)stats["lp"] / (float)stats["nextLp"], "linear-gradient(90deg,#F5A623,#FFE08A)"); lp.style.marginTop = 12; lp.style.height = 10; rank.Add(lp);
            Label Bold12(string text) { var label = v.Text(text, 12, false, "mt"); label.style.unityFont = v.Font("Baloo2-800"); return MenuView.CssLine(label); } // 800 12px Baloo 2
            int lpNow = (int)stats["lp"], lpNext = (int)stats["nextLp"]; string toGo = v.N(System.Math.Max(0, lpNext - lpNow));
            string nextLabel = nextLeague == null ? v.L("En üst lig", "Top league")
                : v.L(LeagueName(v, nextLeague) + (nextLeague.EndsWith("2") ? "'ye " : "'e ") + toGo + " LP", toGo + " LP to " + LeagueName(v, nextLeague));
            var rankFooter = v.Row(Bold12(v.N(lpNow) + " / " + v.N(lpNext) + " LP"), PanelUi.Spacer(v), Bold12(nextLabel)); rankFooter.style.marginTop = 12; rank.Add(rankFooter); middle.Add(rank);
            var win = v.Box("g-panel-card"); win.name = "profile-mode-win-rates"; PanelUi.Pad(win, 14, 18); win.style.flexGrow = 1; win.style.flexBasis = 0; win.Add(PanelUi.Caption(v, v.L("MODLARA GÖRE KAZANMA", "WIN RATE BY MODE"), 10, 1.2f));
            var rates = (JArray)stats["modeWinRates"];
            // G101-Menu: rows named after the v3 modes (Klasik, Dereceli, Botlara karşı; the fourth rate has no v3 mode and
            // no name), bars in MC = #3D7BFF, #FF6B6B, #8B5CF6, #16B6A8.
            string[] names = { v.L("Klasik", "Classic"), v.L("Dereceli", "Ranked"), v.L("Botlara karşı", "Vs bots"), "" };
            string[] colors = { "#3D7BFF", "#FF6B6B", "#8B5CF6", "#16B6A8" };
            for (var i = 0; i < 4; i++)
            {
                var name = v.Text(names[i], 13, true); name.style.width = 86; name.style.height = 16; name.style.unityTextAlign = TextAnchor.MiddleLeft; name.style.marginRight = 10;
                // The fill is width: {{ m.pct }} — "%44" in Turkish is no CSS length, so the design draws every Turkish bar full.
                var bar = v.Progress(v.Loc.Language == "en" ? (float)rates[i] / 100 : 1, colors[i]); PanelUi.Grow(bar); bar.style.height = 8; bar.style.marginRight = 10;
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
                var button = v.Button("", async () =>
                {
                    if (locked) return;
                    if (await v.Services.Profile.SaveAsync((string)v.Model.Profile["name"], (string)v.Model.Profile["color"], (string)v.Model.Profile["status"], frame) != null) PanelUi.SaveFailure(v);
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
            var number = MenuView.TabularDigits(v.Text(value, 28, true, color)); number.style.height = 30.8f; number.style.unityTextAlign = TextAnchor.MiddleLeft; number.style.marginTop = 4; card.Add(number);
            var description = MenuView.CssLine(v.Text(sub, 12, false, "mt")); description.style.marginTop = 4; card.Add(description); return card;
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

        private static string LeagueName(MenuView v, string code) => v.Model.Content["leaderboard"]?["leagues"]?[v.Loc.Language]?[code]?.ToString() ?? code;

        /// <summary>The division on the gem: III / II / I (Galaksi has none).</summary>
        private static string Numeral(string code) => code == "gx" ? "" : code.EndsWith("3") ? "III" : code.EndsWith("2") ? "II" : "I";

        /// <summary>Gem fill, glow and name colour by league: Bronz, Gümüş, Altın, Elmas, Usta, Galaksi.</summary>
        private static (string Fill, string Glow, string Ink) LeagueTint(string code)
        {
            if (code == "gx") return ("linear-gradient(135deg,#8C6EFF,#3DF5E0)", "rgba(140,110,255,.55)", "#B79CFF");
            switch (code[0])
            {
                case 'm': return ("linear-gradient(135deg,#FFB3C0,#D9435A)", "rgba(255,110,140,.5)", "#FF8A9E");
                case 'b': return ("linear-gradient(135deg,#F2C49B,#B8733F)", "rgba(224,150,95,.45)", "#E6A271");
                case 's': return ("linear-gradient(135deg,#F4F7FC,#9AA6BC)", "rgba(210,220,236,.45)", "#D9E0EC");
                case 'd': return ("linear-gradient(135deg,#D8F8FF,#4FC3E8)", "rgba(127,227,255,.5)", "#7FE3FF");
                default: return ("linear-gradient(135deg,#FFE08A,#F5A623)", "rgba(255,201,74,.5)", "#FFC94A");
            }
        }

        /// <summary>The Turkish dative after a number: 25'e, 26'ya, 27'ye, 30'a, 100'e.</summary>
        private static string Dative(int n)
        {
            string[] units = { null, "'e", "'ye", "'e", "'e", "'e", "'ya", "'ye", "'e", "'a" }, tens = { null, "'a", "'ye", "'a", "'a", "'ye", "'a", "'e", "'e", "'a" };
            if (n % 10 != 0) return units[n % 10];
            if (n % 100 != 0) return tens[n / 10 % 10];
            return "'e"; // yüz, bin
        }

        private static void History(MenuView v, VisualElement parent)
        {
            var header = v.Row(v.Box(), PanelUi.Caption(v, v.L("MOD", "MODE"), 10, 1.2f), PanelUi.Caption(v, v.L("SIRA", "PLACE"), 10, 1.2f), PanelUi.Caption(v, v.L("ÇİP", "CHIPS"), 10, 1.2f), PanelUi.Caption(v, v.L("ZAMAN", "WHEN"), 10, 1.2f)); HistoryColumns(header); header.style.marginBottom = 12; parent.Add(header);
            foreach (JObject match in v.Services.Profile.GetHistory())
            {
                var mode = (JObject)v.Model.Content["modes"][(int)match["mode"]];
                var art = PanelUi.Badge(v, (string)mode["glyph"], (string)mode["bg"], "#fff", 14); art.style.width = art.style.height = 30; art.style.justifyContent = Justify.Center;
                PanelUi.Pad(art, 0, 0); var artColumn = v.Box(); artColumn.Add(art);
                long stake = (long?)match["stake"] ?? 5000; string table = stake >= 1000 ? stake / 1000 + "K" : stake.ToString();
                var details = v.Column(v.Text(v.L((string)mode["tr"], (string)mode["en"]), 15, true), v.Text((string)match["lp"] ?? (stake > 0 ? v.L("101 · " + table + " masa", "101 · " + table + " table") : v.L("101 · bahissiz", "101 · no stake")), 12, false, "mt"));
                var place = (int)match["place"];
                var rank = v.Box(); rank.Add(PanelUi.Badge(v, v.L(place + ".", "#" + place), place == 1 ? "rgba(255,201,74,.18)" : "dp", place == 1 ? "#FFC94A" : "mt", 11)); rank.style.alignItems = Align.FlexStart;
                var delta = (long)match["delta"];
                var minutes = (int)match["minutes"];
                var ago = minutes < 60 ? v.L(minutes + " dk önce", minutes + " min ago") : minutes < 1440 ? v.L(minutes / 60 + " sa önce", minutes / 60 + " h ago")
                    : minutes < 2880 ? v.L("Dün", "Yesterday") : v.L(minutes / 1440 + " gün önce", minutes / 1440 + " days ago");
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
