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
            var big = v.Avatar(124, null, true); big.Q<Label>("avatar-initials").name = "menu.me.ini#3"; big.Q<Label>(className: "avatar-level").name = "menu.mp.lv"; summary.Add(big);
            var name = v.Text((string)v.Model.Profile["name"], 24, true); name.name = "menu.me.name#3"; name.style.marginTop = 18; name.style.height = 24 * 1.15f; name.style.unityTextAlign = TextAnchor.MiddleCenter; summary.Add(name);
            var status = (string)v.Model.Profile["status"];
            var statusLabel = PanelUi.Wrap(v, string.IsNullOrEmpty(status) ? v.L("Henüz durum mesajı yok", "No status message yet") : status, 13);
            statusLabel.name = "menu.mp.status"; statusLabel.style.unityTextAlign = TextAnchor.MiddleCenter; statusLabel.style.marginTop = 10; statusLabel.style.minHeight = 18; MenuView.CssParagraph(statusLabel, 13 * 1.4f, false); summary.Add(statusLabel);
            // font: 800 12px Baloo 2; the value in tabular digits.
            Label Bold12(string text) { var label = v.Text(text, 12, false, "mt"); label.style.unityFont = v.Font("Baloo2-800"); return MenuView.CssLine(label); }
            int nextLevel = ((int?)stats["level"] ?? v.Model.Level) + 1;
            var xpLabel = Bold12(v.L("Seviye " + nextLevel + Dative(nextLevel), "To level " + nextLevel)); xpLabel.name = "menu.mp.xpLabel";
            var xpValue = MenuView.TabularDigits(Bold12(v.N((int)stats["xp"]) + " / " + v.N((int)stats["nextXp"]) + " XP")); xpValue.name = "menu.mp.xp";
            var xp = v.Row(xpLabel, PanelUi.Spacer(v), xpValue); xp.style.alignSelf = Align.Stretch; xp.style.marginTop = 14; summary.Add(xp);
            var progress = v.Progress((float)stats["xp"] / (float)stats["nextXp"]); progress.style.height = 10; progress.style.alignSelf = Align.Stretch; progress.style.marginTop = 6; summary.Add(progress);
            // Reference: dashed strip, padding 10px 12px, radius 14, gap 10; code 700 16px, "Kopyala" 700 13px heading in ic.
            var code = PanelUi.CodeStrip(v, 12, 16, 14); code[0].name = "menu.fr.codeLabel"; PanelUi.Pad(code, 10, 12); PanelUi.Gap(code, 10); code.style.alignSelf = Align.Stretch; code.style.marginTop = 14; code.Add(PanelUi.Spacer(v));
            Button copy = null;
            copy = PanelUi.CustomButton(v, v.L("Kopyala", "Copy"), () =>
            {
                v.Copy(v.Model.FriendCode); copy.text = v.L("Kopyalandı", "Copied");
                copy.schedule.Execute(() => copy.text = v.L("Kopyala", "Copy")).StartingIn(1500);
            }, ""); copy.name = "menu.af.copy";
            copy.style.paddingLeft = copy.style.paddingRight = copy.style.paddingTop = copy.style.paddingBottom = 0; copy.style.fontSize = 13; copy.style.color = v.C("ic");
            copy.RegisterCallback<PointerEnterEvent>(_ => copy.style.color = UnityEngine.Color.white); copy.RegisterCallback<PointerLeaveEvent>(_ => copy.style.color = v.C("ic"));
            code.Add(copy); summary.Add(code);
            summary.Add(PanelUi.Spacer(v));
            var edit = PanelUi.Action(v, v.L("Profili düzenle", "Edit profile"), () => v.OpenDialog("profile")); edit.name = "menu.openProfile#2"; PanelUi.Grow(edit);
            edit.style.flexDirection = FlexDirection.Row; PanelUi.Pad(edit, 12, 0);
            var pencil = v.Icon("edit", 16, "aT"); pencil.style.marginRight = 8; edit.Insert(0, pencil);
            var share = v.Button("", () => v.OpenDialog("share"), "secondary"); share.name = "menu.openShare"; share.tooltip = v.L("Profili paylaş", "Share profile"); share.style.width = share.style.height = 46; share.style.minWidth = 46; share.style.paddingLeft = share.style.paddingRight = 0; share.style.marginLeft = 10; share.Add(v.Icon("share", 20));
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
            // mp.stats: "Bu ay +%3" — the month's change, when the account has one (the sample's +3).
            int? month = (int?)stats["winRateMonth"];
            row.Add(Stat(v, v.L("KAZANMA", "WIN RATE"), v.P((int)stats["winRate"]), month.HasValue ? v.L("Bu ay " + (month >= 0 ? "+" : "−") + v.P(System.Math.Abs(month.Value)), (month >= 0 ? "+" : "−") + v.P(System.Math.Abs(month.Value)) + " this month") : v.L("tüm modlarda", "across modes"), "#3CCB6E"));
            row.Add(Stat(v, v.L("EN İYİ AÇILIŞ", "BEST OPENING"), v.N((int)stats["bestOpening"]), v.L("puan", "points"), "a1"));
            row.Add(Stat(v, v.L("OKEY BİTİŞ", "OKEY FINISH"), v.N((int)stats["okeyFinish"]), v.L("katlanan el", "doubled hands"), "#F4F1E8"));
            for (var i = 0; i < row.childCount; i++) { row[i][0].name = "menu.x.k[" + i + "]"; row[i][1].name = "menu.x.v[" + i + "]"; row[i][2].name = "menu.x.sub[" + i + "]"; }
            PanelUi.Gap(row, 10); parent.Add(row);
            var middle = v.Row(); middle.style.marginTop = 14; middle.style.alignItems = Align.Stretch;
            var rank = v.Box("g-panel-card"); rank.name = "profile-rank"; PanelUi.Pad(rank, 16, 18); rank.style.flexGrow = 1.1f; rank.style.flexBasis = 0; rank.style.marginRight = 10;
            // rk (renderVals): the account's step and LP, else the prototype's sample (idx 7, 72 LP, peak 7): G101Emblem 60, the
            // league name in rk.txt, SEZON EN İYİ the peak's label, the bar rk.pct in rk.bar, "72 / 100 LP" and "Altın I'e 28 LP".
            bool sample = stats["league"] == null;
            int idx = v.CurrentRankIndex(), peak = v.PeakRankIndex(), lp = sample ? MenuView.SampleRankLp : (int)stats["lp"]; bool galaxy = idx >= 15;
            var emblem = v.Emblem(idx, 60); emblem.style.marginRight = 14;
            int seasonNumber = (int?)stats["season"] ?? 3, place = (int?)stats["seasonRank"] ?? 0;
            string shown = v.RankLabel(idx) + (galaxy && place > 0 ? " · #" + place : "");
            var seasonLabel = PanelUi.Caption(v, v.L("SEZON " + seasonNumber + " · DERECELİ", "SEASON " + seasonNumber + " · RANKED"), 10, 1.2f); seasonLabel.name = "menu.mp.season";
            var rankLabel = MenuView.CssLine(v.Text(shown, 22, true, MenuView.RankInk(idx)), 22 * 1.15f); rankLabel.name = "menu.t.rank#2";
            var description = v.Column(seasonLabel, rankLabel); PanelUi.Grow(description);
            var bestLabel = PanelUi.Caption(v, v.L("SEZON EN İYİ", "SEASON BEST"), 10, 1.2f); bestLabel.name = "menu.mp.bestLabel";
            var bestValue = v.Text(v.RankLabel(peak), 15, true); bestValue.name = "menu.rk.best";
            var best = v.Column(bestLabel, bestValue); best.style.alignItems = Align.FlexEnd;
            rank.Add(v.Row(emblem, description, best));
            var lpBar = v.Progress(galaxy ? 1 : lp / 100f, (string)MenuView.RankAt(idx).League["bar"]); lpBar.style.marginTop = 12; lpBar.style.height = 10; rank.Add(lpBar);
            Label Bold12(string text) { var label = v.Text(text, 12, false, "mt"); label.style.unityFont = v.Font("Baloo2-800"); return MenuView.CssLine(label); } // 800 12px Baloo 2
            bool en = v.Loc.Language == "en";
            string next = galaxy ? v.L("İlk 100", "Top 100") : idx == 14 ? v.L("Galaksi'ye " + (100 - lp) + " LP", (100 - lp) + " LP to Galaxy")
                : en ? (100 - lp) + " LP to " + (string)MenuView.RankAt(idx + 1).League["en"] + (MenuView.RankAt(idx + 1).Tier > 0 ? " " + MenuView.Roman[MenuView.RankAt(idx + 1).Tier] : "")
                : (string)MenuView.RankAt(idx + 1).League["tr"] + (MenuView.RankAt(idx + 1).Tier > 0 ? " " + MenuView.Roman[MenuView.RankAt(idx + 1).Tier] : "") + "'e " + (100 - lp) + " LP";
            var lpLabel = Bold12(galaxy ? lp + " LP" : lp + " / 100 LP"); lpLabel.name = "menu.rk.lp";
            var nextLabel = Bold12(next); nextLabel.name = "menu.rk.next";
            var rankFooter = v.Row(lpLabel, PanelUi.Spacer(v), nextLabel); rankFooter.style.marginTop = 12; rank.Add(rankFooter); middle.Add(rank);
            var win = v.Box("g-panel-card"); win.name = "profile-mode-win-rates"; PanelUi.Pad(win, 14, 18); win.style.flexGrow = 1; win.style.flexBasis = 0; var byMode = PanelUi.Caption(v, v.L("MODLARA GÖRE KAZANMA", "WIN RATE BY MODE"), 10, 1.2f); byMode.name = "menu.mp.byMode"; win.Add(byMode);
            var rates = (JArray)stats["modeWinRates"];
            // G101-Menu: rows named after the v3 modes (Klasik, Dereceli, Botlara karşı; the fourth rate has no v3 mode and
            // no name), bars in MC = #3D7BFF, #FF6B6B, #8B5CF6, #16B6A8.
            string[] names = { v.L("Klasik", "Classic"), v.L("Dereceli", "Ranked"), v.L("Botlara karşı", "Vs bots"), "" };
            string[] colors = { "#3D7BFF", "#FF6B6B", "#8B5CF6", "#16B6A8" };
            for (var i = 0; i < 4; i++)
            {
                var name = v.Text(names[i], 13, true); name.name = "menu.m.name[" + i + "]"; name.style.width = 86; name.style.height = 16; name.style.unityTextAlign = TextAnchor.MiddleLeft; name.style.marginRight = 10;
                // The fill is width: {{ m.pct }} — "%44" in Turkish is no CSS length, so the design draws every Turkish bar full.
                var bar = v.Progress(v.Loc.Language == "en" ? (float)rates[i] / 100 : 1, colors[i]); PanelUi.Grow(bar); bar.style.height = 8; bar.style.marginRight = 10;
                var value = v.Text(v.P((int)rates[i]), 13, true); value.name = "menu.m.pct[" + i + "]"; value.style.width = 40; value.style.height = 16; value.style.unityTextAlign = TextAnchor.MiddleRight;
                var line = v.Row(name, bar, value); line.style.marginTop = 9; win.Add(line);
            }
            middle.Add(win); parent.Add(middle);
            // mp.frames (frameOpts): Klasik + six league frames, grid 7 × minmax(0,1fr), gap 8; a tile: column, gap 5, padding
            // 9px 4px 8px, radius 14, --pnH + inset 2px --a1 when worn (else --dp), opacity .5 when locked; the 44px chip, the
            // name 700 12px/1.1 and the state 800 10px (Takılı #8EF0B0 · Sahipsin · Kilitli); title = the longer state.
            var frames = v.Box("g-panel-card"); frames.name = "profile-frames"; PanelUi.Pad(frames, 14, 18); frames.style.marginTop = 14;
            var framesLabel = PanelUi.Caption(v, v.L("ÇERÇEVELER", "FRAMES"), 10, 1.2f); framesLabel.name = "menu.mp.framesLabel";
            var framesHint = v.Text(v.L("Lig çerçeveleri derecelide açılır", "League frames unlock in ranked"), 12, false, "mt"); framesHint.name = "menu.mp.framesHint"; framesHint.style.whiteSpace = WhiteSpace.NoWrap;
            frames.Add(v.Row(framesLabel, PanelUi.Spacer(v), framesHint));
            var frameRow = v.Row(); frameRow.style.marginTop = 10; frameRow.style.alignItems = Align.Stretch;
            int worn = MenuView.FrameId((int?)v.Model.Profile["frame"] ?? 0);
            for (var i = 0; i < MenuView.FrameIds.Length; i++)
            {
                int id = MenuView.FrameIds[i]; bool locked = MenuView.FrameLocked(id, peak), equipped = worn == id;
                var league = id >= 10 ? (JObject)MenuView.Leagues[id - 10] : null; string lname = league == null ? v.L("Klasik", "Classic") : (string)league[en ? "en" : "tr"];
                var button = PanelUi.CustomButton(v, "", async () =>
                {
                    if (locked || equipped) return;
                    // A saved 1 is the Altın league frame; the other league frames cannot be stored yet (EKSIKLER EK-603).
                    if (await v.Services.Profile.SaveAsync((string)v.Model.Profile["name"], (string)v.Model.Profile["color"], (string)v.Model.Profile["status"], id == 12 ? 1 : id) != null) PanelUi.SaveFailure(v);
                }, "g-frame-tile");
                button.name = "menu.f.pick[" + i + "]"; button.style.flexGrow = 1; button.style.flexBasis = 0; button.style.minWidth = 0; button.style.flexDirection = FlexDirection.Column; button.style.alignItems = Align.Center; button.style.justifyContent = Justify.FlexStart;
                button.style.paddingTop = 9; button.style.paddingBottom = 8; button.style.paddingLeft = button.style.paddingRight = 4; if (i < MenuView.FrameIds.Length - 1) button.style.marginRight = 8;
                v.Decorate(button, equipped ? "pnH" : "dp", 14); if (equipped) button.Add(PanelUi.Ring(v, 2, "a1", 14));
                else { button.RegisterCallback<PointerEnterEvent>(_ => v.Decorate(button, "pnH", 14)); button.RegisterCallback<PointerLeaveEvent>(_ => v.Decorate(button, "dp", 14)); }
                if (locked) { button.SetEnabled(false); button.style.opacity = .5f; }
                button.tooltip = locked ? (en ? "Unlocks at " + (string)league["en"] : (string)league["tr"] + " liginde açılır") : equipped ? v.L("Takılı", "Equipped") : v.L("Sahipsin", "Owned");
                var chip = v.Avatar(44, new JObject { ["name"] = v.Model.Profile["name"], ["color"] = v.Model.Profile["color"], ["frame"] = id }); button.Add(chip);
                var frameName = MenuView.CssLine(v.Text(lname, 12, true), 12 * 1.1f); frameName.name = "menu.f.name[" + i + "]"; frameName.style.marginTop = 5; frameName.style.whiteSpace = WhiteSpace.NoWrap; frameName.style.unityTextAlign = TextAnchor.MiddleCenter;
                var frameState = v.Text(locked ? v.L("Kilitli", "Locked") : equipped ? v.L("Takılı", "Equipped") : v.L("Sahipsin", "Owned"), 10, false, equipped ? "#8EF0B0" : "mt"); frameState.name = "menu.f.short[" + i + "]";
                frameState.style.unityFont = v.Font("Baloo2-800"); frameState.style.marginTop = 5; frameState.style.whiteSpace = WhiteSpace.NoWrap; MenuView.CssLine(frameState); frameState.style.unityTextAlign = TextAnchor.MiddleCenter;
                button.Add(frameName); button.Add(frameState); frameRow.Add(button);
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
            // grid repeat(3, minmax(0,1fr)), gap 10; a card: flex, align-items centre, gap 12, padding 14, radius 18, --sd,
            // opacity .75 when locked — the cards of a row share its height and centre their content in it.
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
                var card = v.Row(); card.AddToClassList("g-profile-badge"); card.style.alignItems = Align.Center;
                if (index % 3 < 2) card.style.marginRight = 10;
                if (locked) card.style.opacity = .75f;
                // 46px tile, radius 14: locked --dp inside a 1px rgba(255,255,255,.08) ring; else the colour's gradient,
                // 0 3px 0 rgba(0,0,0,.3) and a 0 0 16px glow; 700 18px glyph.
                var art = v.Box(); art.name = "menu.b.g[" + index + "]"; art.style.width = art.style.height = 46; art.style.flexShrink = 0;
                art.style.alignItems = Align.Center; art.style.justifyContent = Justify.Center; art.style.marginRight = 12;
                var color = (string)badge["color"];
                v.Decorate(art, locked ? "dp" : "linear-gradient(160deg," + color + "," + color + "AA)", 14, locked ? 0 : 3, "rgba(0,0,0,.3)");
                if (locked) art.Add(PanelUi.Ring(v, 1, "rgba(255,255,255,.08)", 14));
                else { var glow = v.C(color); glow.a = .4f; GalaxyFilters.Shadow(art, 0, 0, 8, glow); }
                art.Add(v.Text((string)badge["glyph"], 18, true, "#fff"));
                // 700 15px/1.2 name over a 700 12px description (line-height normal, 19.2 px) 2px below.
                var name = PanelUi.Wrap(v, v.L((string)badge["tr"], (string)badge["en"]), 15, "#F4F1E8", true); name.name = "menu.b.name[" + index + "]"; MenuView.CssLines(name, 18);
                var description = PanelUi.Wrap(v, v.L((string)badge["desc_tr"], (string)badge["desc_en"]), 12); description.name = "menu.b.desc[" + index + "]"; description.style.marginTop = 2; MenuView.CssLines(description, 19.2f);
                var text = v.Column(name, description); PanelUi.Grow(text);
                if (locked)
                {
                    // margin-top 6, gap 8: a 6px bar (radius 3, --dp, a square --mt fill) and the count, 800 11px.
                    var progress = v.Progress((float)badge["cur"] / (float)badge["max"], "mt"); progress.style.height = 6; PanelUi.Grow(progress); progress.style.marginRight = 8;
                    var fill = progress.Q<VisualElement>(className: "progress-fill");
                    fill.style.borderTopLeftRadius = fill.style.borderTopRightRadius = fill.style.borderBottomLeftRadius = fill.style.borderBottomRightRadius = 0;
                    var count = v.Text(v.N((int)badge["cur"]) + "/" + v.N((int)badge["max"]), 11, false, "mt"); count.name = "menu.b.prog[" + index + "]"; count.style.unityFont = v.Font("Baloo2-800"); MenuView.CssLine(count);
                    var row = v.Row(progress, count); row.style.marginTop = 6; text.Add(row);
                }
                card.Add(art); card.Add(text); badgeRow.Add(card); index++;
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
            // Header: grid 44px 1fr 110px 110px 90px, padding 0 14px 2px, 900 10px body, 1.2px tracking.
            Label Head(string tr, string en, string id) { var l = PanelUi.Caption(v, v.L(tr, en), 10, 1.2f); l.name = id; return l; }
            var header = v.Row(v.Box(), Head("MOD", "MODE", "menu.mp.hMode"), Head("SIRA", "PLACE", "menu.mp.hPlace"), Head("ÇİP", "CHIPS", "menu.mp.hChips"), Head("ZAMAN", "WHEN", "menu.mp.hWhen"));
            HistoryColumns(header); header.style.paddingLeft = header.style.paddingRight = 14; header.style.paddingBottom = 2; parent.Add(header);
            // mp.history (HS): glyph ['1','2','×2','II'][m] on MC[m]; the name is menu.STR.modes[m] — Klasik, Dereceli, Botlara
            // karşı, and nothing for m 3 (EKSIKLER EK-606); detail lp || "101 · 5K masa".
            string[] glyphs = { "1", "2", "×2", "II" }, tints = { "#3D7BFF", "#FF6B6B", "#8B5CF6", "#16B6A8" }, names = { v.L("Klasik", "Classic"), v.L("Dereceli", "Ranked"), v.L("Botlara karşı", "Vs bots") };
            int index = 0;
            foreach (JObject match in v.Services.Profile.GetHistory())
            {
                int m = (int)match["mode"]; int i = index++;
                var mode = (JObject)v.Model.Content["modes"][m];
                var art = v.Box(); art.style.width = art.style.height = 30; art.style.alignItems = Align.Center; art.style.justifyContent = Justify.Center; v.Decorate(art, m < 4 ? tints[m] : (string)mode["bg"], 9);
                var glyph = v.Text(m < 4 ? glyphs[m] : (string)mode["glyph"], 13, true, "#fff"); glyph.name = "menu.h.g[" + i + "]"; art.Add(glyph);
                var artColumn = v.Box(); artColumn.Add(art);
                long stake = (long?)match["stake"] ?? 5000; string table = stake >= 1000 ? stake / 1000 + "K" : stake.ToString();
                var modeName = MenuView.CssLine(v.Text(m < names.Length ? names[m] : "", 15, true), 18); modeName.name = "menu.h.mode[" + i + "]";
                var detail = MenuView.CssLine(v.Text((string)match["lp"] ?? (stake > 0 ? v.L("101 · " + table + " masa", "101 · " + table + " table") : v.L("101 · bahissiz", "101 · no stake")), 12, false, "mt")); detail.name = "menu.h.detail[" + i + "]";
                var details = v.Column(); if (modeName.text.Length > 0) details.Add(modeName); details.Add(detail);
                var place = (int)match["place"];
                var badge = v.Text(v.L(place + ".", "#" + place), 12, false, place == 1 ? "#FFC94A" : "mt"); badge.name = "menu.h.place[" + i + "]"; badge.style.unityFont = v.Font("Baloo2-800"); MenuView.CssLine(badge);
                badge.style.paddingTop = badge.style.paddingBottom = 3; badge.style.paddingLeft = badge.style.paddingRight = 9; badge.style.height = StyleKeyword.Auto; v.Decorate(badge, place == 1 ? "rgba(255,201,74,.18)" : "dp", 8);
                var rank = v.Row(badge); rank.style.alignItems = Align.Center;
                var delta = (long)match["delta"];
                var deltaText = MenuView.TabularDigits(v.Text(delta == 0 ? "—" : (delta > 0 ? "+" : "−") + v.N(System.Math.Abs(delta)), 15, true, delta > 0 ? "#8EF0B0" : delta < 0 ? "#FF8A94" : "mt")); deltaText.name = "menu.h.delta[" + i + "]";
                var minutes = (int)match["minutes"];
                var ago = v.Text(minutes < 60 ? v.L(minutes + " dk önce", minutes + " min ago") : minutes < 1440 ? v.L(minutes / 60 + " sa önce", minutes / 60 + " h ago") : v.L("Dün", "Yesterday"), 12, false, "mt"); ago.name = "menu.h.when[" + i + "]";
                // padding 9px 14px, radius 14, --sd; the list's gap 14.
                var row = v.Row(artColumn, details, rank, deltaText, ago); PanelUi.Pad(row, 9, 14); v.Decorate(row, "sd", 14); row.style.marginTop = 14; row.style.flexShrink = 0; HistoryColumns(row); parent.Add(row);
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
