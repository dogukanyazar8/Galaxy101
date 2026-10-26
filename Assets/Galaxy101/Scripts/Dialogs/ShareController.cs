using System.Linq;
using UnityEngine.UIElements;

namespace Galaxy101
{
    public sealed class ShareController
    {
        public VisualElement Build(MenuView v)
        {
            var root = PanelUi.Shell(v, "ShareProfile");
            PanelUi.Header(v, root, v.L("Profili paylaş", "Share profile"), v.L("Profil kartını arkadaşlarına gönder", "Send your profile card to friends"), "share", "linear-gradient(160deg,#FFA27A,#F0683A)", 22);
            root.Q<VisualElement>("footer").style.display = DisplayStyle.None;
            var shHeader = root.Q<VisualElement>("header"); var shTexts = shHeader.Query<Label>().ToList(); shTexts[0].name = "menu.sh.title"; shTexts[1].name = "menu.sh.sub"; shHeader.Q<Button>("dialog-close").name = "menu.closeDlg#6";
            var body = root.Q<VisualElement>("content");
            // height 180, padding 0 26px, gap 20, radius 22.
            var card = v.Row(); card.AddToClassList("g-share-card"); card.style.paddingLeft = card.style.paddingRight = 26; card.style.paddingTop = card.style.paddingBottom = 0;
            v.Decorate(card, "radial-gradient(ellipse 70% 120% at 85% 50%,var(--bg1),var(--bg2) 55%,var(--bg3))", 22);
            var planet = PanelUi.Circle(v, 220, "radial-gradient(circle at 35% 30%,var(--lg1),var(--a2) 55%,var(--aS))"); planet.style.position = Position.Absolute; planet.style.right = -40; planet.style.top = -30; planet.style.opacity = .35f; card.Add(planet);
            foreach (var point in new[] { new UnityEngine.Vector2(550, 18), new UnityEngine.Vector2(490, 58), new UnityEngine.Vector2(410, 28), new UnityEngine.Vector2(520, 138), new UnityEngine.Vector2(350, 148) })
            {
                var star = PanelUi.Circle(v, 3, "st"); star.style.position = Position.Absolute; star.style.left = point.x; star.style.top = point.y; card.Add(star);
            }
            var avatar = v.Avatar(104, null, true); avatar.style.marginRight = 20; card.Add(avatar);
            // mp.lv#2 reads mp.lv ("SEVİYE 24") at 900 11px.
            var level = avatar.Q<Label>(className: "avatar-level"); level.name = "menu.mp.lv#2"; level.text = v.L("SEVİYE " + v.Model.Level, "LEVEL " + v.Model.Level); avatar.Q<Label>("avatar-initials").name = "menu.me.ini#4";
            var stats = v.Services.Profile.GetStats();
            var shName = MenuView.CssLine(v.Text((string)v.Model.Profile["name"], 26, true), 26 * 1.1f); shName.name = "menu.me.name#4";
            var info = v.Column(shName); PanelUi.Grow(info);
            // The share card keeps the v3 marker: a 14px gold diamond, t.rank in #FFC94A, "· {{ sh.season }}" (two text runs).
            var rankText = v.Text(v.RankLabel(v.CurrentRankIndex()), 15, true, "#FFC94A"); rankText.name = "menu.t.rank#3";
            var dot = v.Text("· ", 13, false, "mt"); dot.style.whiteSpace = WhiteSpace.Pre; var season = v.Text(v.L("Sezon 3", "Season 3"), 13, false, "mt");
            var league = v.Row(PanelUi.Diamond(v, 14, "linear-gradient(135deg,#FFE08A,#F5A623)", 3), rankText, v.Row(dot, season)); PanelUi.Gap(league, 8); league.style.marginTop = 8; info.Add(league);
            var mini = v.Row(); mini.style.marginTop = 12; // gap 8 + margin-top 4
            var titles = new[] { v.L("EL", "HANDS"), v.L("KAZANMA", "WIN"), v.L("EN İYİ", "BEST") };
            var numbers = new[] { v.N((int)stats["hands"]), v.P((int)stats["winRate"]), v.N((int)stats["bestOpening"]) };
            for (var i = 0; i < 3; i++)
            {
                var value = MenuView.CssLine(v.Text(numbers[i], 16, true, i == 1 ? "#8EF0B0" : i == 2 ? "a1" : "#F4F1E8"), 16 * 1.1f); value.name = "menu.c.v[" + i + "]";
                var key = MenuView.CssLine(PanelUi.Caption(v, titles[i], 9, 1)); key.name = "menu.c.k[" + i + "]";
                var box = v.Column(value, key); PanelUi.Pad(box, 6, 10); v.Decorate(box, "rgba(0,0,0,.28)", 10); if (i < 2) box.style.marginRight = 8; mini.Add(box);
            }
            info.Add(mini); card.Add(info);
            var galaxy = v.Text("GALAXY", 15, true, "a1"); MenuView.Tracking(galaxy, 2, 15);
            var numeral = PanelUi.Badge(v, "101", "a1", "aT", 11); PanelUi.Pad(numeral, 1, 5);
            var logo = v.Row(galaxy, numeral); PanelUi.Gap(logo, 6); logo.style.position = Position.Absolute; logo.style.right = 20; logo.style.bottom = 14; card.Add(logo); body.Add(card);
            var linkLabel = MenuView.CssLine(v.Text(v.L("Profil bağlantısı", "Profile link"), 12, false, "mt")); linkLabel.name = "menu.sh.linkLabel"; linkLabel.style.unityFont = v.Font("Baloo2-800"); linkLabel.style.marginTop = 16; linkLabel.style.marginBottom = 6; body.Add(linkLabel);
            var url = v.Row(v.Icon("link", 16, "mt"), v.Text(v.Services.Share.ProfileUrl.Replace("https://", ""), 15)); PanelUi.Gap(url, 10); PanelUi.Grow(url); PanelUi.Pad(url, 0, 14); v.Decorate(url, "dp", 14); url.style.height = 48;
            Button copy = null;
            // sh.copy: 180 × 48, 700 15px; copied (1.8 s): #3CCB6E on #22914A, white, a 15px tick before "Kopyalandı" (gap 8).
            VisualElement tick = null;
            copy = PanelUi.Action(v, v.L("Bağlantıyı kopyala", "Copy link"), () =>
            {
                v.Services.Share.CopyToClipboard(); Copied(v, copy, true);
                copy.schedule.Execute(() => Copied(v, copy, false)).StartingIn(1800);
            }, "primary bright-120");
            copy.name = "menu.sh.copy"; copy.style.width = 180; copy.style.height = 48; copy.style.marginLeft = 10; copy.style.flexDirection = FlexDirection.Row; copy.style.fontSize = 15;
            tick = v.Icon("tick", 15, "#fff"); tick.name = "share-copied-tick"; tick.style.marginRight = 8; tick.style.display = DisplayStyle.None; copy.Insert(0, tick);
            body.Add(v.Row(url, copy));
            var via = MenuView.CssLine(v.Text(v.L("Şununla paylaş", "Share via"), 12, false, "mt")); via.name = "menu.sh.viaLabel"; via.style.unityFont = v.Font("Baloo2-800"); via.style.marginTop = 16; via.style.marginBottom = 8; body.Add(via);
            var targets = v.Row(); targets.name = "share-targets"; targets.style.alignItems = Align.Stretch;
            var names = new[] { "WhatsApp", "Telegram", "X", "Instagram", v.L("Diğer", "More") };
            var glyphs = new[] { "W", "T", "X", "IG", "•••" };
            var colors = new[] { "#25D366", "#2AABEE", "#2A2F3A", "linear-gradient(135deg,#F9A13A,#E1306C 55%,#8A3AB9)", "pnH" };
            var shades = new[] { "#128C4A", "#1A7DB0", "#0E1116", "#7A2A8A", "pnS" };
            for (var i = 0; i < 5; i++)
            {
                var platform = i == 4 ? "More" : names[i]; var name = names[i];
                var button = PanelUi.CustomButton(v, "", () =>
                {
                    v.Services.Share.ShareTo(platform);
                    v.Toast(v.L(name + " açılıyor…", "Opening " + name + "…"));
                }, "g-share-target");
                button.name = "menu.a.go[" + i + "]";
                // 46px tile, radius 14, box-shadow 0 3px 0 shade; 700 18px glyph; gap 8; 700 13px name.
                var icon = v.Box(); icon.style.width = icon.style.height = 46; icon.style.flexShrink = 0; icon.style.alignItems = Align.Center; icon.style.justifyContent = Justify.Center; icon.style.marginBottom = 8; v.Decorate(icon, colors[i], 14, 3, shades[i]);
                var glyph = MenuView.CssLine(v.Text(glyphs[i], 18, true, "#fff")); glyph.name = "menu.a.g[" + i + "]"; icon.Add(glyph); button.Add(icon);
                var label = v.Text(name, 13, true); label.name = "menu.a.name[" + i + "]"; button.Add(label);
                if (i < 4) button.style.marginRight = 10; targets.Add(button);
            }
            body.Add(targets);
            var inviteTitle = MenuView.CssLine(v.Text(v.L("Arkadaşını davet et, çip kazan", "Invite a friend, earn chips"), 15, true), 15 * 1.25f); inviteTitle.name = "menu.sh.inviteTitle";
            var inviteSub = PanelUi.Wrap(v, v.L("Bağlantınla gelen arkadaşın ilk elini oynayınca ikiniz de 2.000 çip kazanırsınız.", "You both get 2,000 chips when they play their first hand with your link."), 12); inviteSub.name = "menu.sh.inviteSub";
            var inviteText = v.Column(inviteTitle, inviteSub); PanelUi.Grow(inviteText);
            var reward = v.Row(PanelUi.Coin(v, 16), v.Text("+" + v.N(2000), 15, true, "#FFC94A")); PanelUi.Gap(reward, 6); PanelUi.Pad(reward, 6, 12); v.Decorate(reward, "rgba(255,201,74,.14)", 10);
            var invite = v.Row(inviteText, reward); invite.style.marginTop = 16; PanelUi.Pad(invite, 12, 16); v.Decorate(invite, "sd", 16); body.Add(invite);
            return root;
        }

        /// <summary>The copy button's two looks (sh.copied): gold "Bağlantıyı kopyala" / green tick + "Kopyalandı".</summary>
        public static void Copied(MenuView v, Button copy, bool copied)
        {
            copy.text = copied ? v.L("Kopyalandı", "Copied") : v.L("Bağlantıyı kopyala", "Copy link");
            v.Decorate(copy, copied ? "#3CCB6E" : "linear-gradient(var(--a1),var(--a2))", 14, 4, copied ? "#22914A" : "aS"); copy.style.color = v.C(copied ? "#fff" : "aT");
            var tick = copy.Q("share-copied-tick"); if (tick != null) tick.style.display = copied ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }
}
