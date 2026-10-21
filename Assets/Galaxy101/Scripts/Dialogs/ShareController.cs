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
            var body = root.Q<VisualElement>("content");
            var card = v.Row(); card.AddToClassList("g-share-card");
            v.Decorate(card, "radial-gradient(ellipse 70% 120% at 85% 50%,var(--bg1),var(--bg2) 55%,var(--bg3))", 22);
            var planet = PanelUi.Circle(v, 220, "radial-gradient(circle at 35% 30%,var(--lg1),var(--a2) 55%,var(--aS))"); planet.style.position = Position.Absolute; planet.style.right = -40; planet.style.top = -30; planet.style.opacity = .35f; card.Add(planet);
            foreach (var point in new[] { new UnityEngine.Vector2(550, 18), new UnityEngine.Vector2(490, 58), new UnityEngine.Vector2(410, 28), new UnityEngine.Vector2(520, 138), new UnityEngine.Vector2(350, 148) })
            {
                var star = PanelUi.Circle(v, 3, "st"); star.style.position = Position.Absolute; star.style.left = point.x; star.style.top = point.y; card.Add(star);
            }
            var avatar = v.Avatar(104, null, true); avatar.style.marginRight = 20; card.Add(avatar);
            var stats = v.Services.Profile.GetStats();
            var info = v.Column(v.Text((string)v.Model.Profile["name"], 26, true)); PanelUi.Grow(info);
            var league = v.Row(PanelUi.Diamond(v, 14, "linear-gradient(135deg,#FFE08A,#F5A623)", 3), v.Text(v.L("Altın II", "Gold II"), 15, true, "#FFC94A"), v.Text("· " + v.L("Sezon 3", "Season 3"), 13, false, "mt")); PanelUi.Gap(league, 8); league.style.marginTop = 8; info.Add(league);
            var mini = v.Row(); mini.style.marginTop = 10;
            var titles = new[] { v.L("EL", "HANDS"), v.L("KAZANMA", "WIN"), v.L("EN İYİ", "BEST") };
            var numbers = new[] { v.N((int)stats["hands"]), v.P((int)stats["winRate"]), v.N((int)stats["bestOpening"]) };
            for (var i = 0; i < 3; i++)
            {
                var box = v.Column(v.Text(numbers[i], 16, true, i == 1 ? "#8EF0B0" : i == 2 ? "a1" : "#F4F1E8"), PanelUi.Caption(v, titles[i], 9, 1)); PanelUi.Pad(box, 6, 10); v.Decorate(box, "rgba(0,0,0,.28)", 10); if (i < 2) box.style.marginRight = 8; mini.Add(box);
            }
            info.Add(mini); card.Add(info);
            var galaxy = v.Text("GALAXY", 15, true, "a1"); MenuView.Tracking(galaxy, 2, 15);
            var numeral = PanelUi.Badge(v, "101", "a1", "aT", 11); PanelUi.Pad(numeral, 1, 5);
            var logo = v.Row(galaxy, numeral); PanelUi.Gap(logo, 6); logo.style.position = Position.Absolute; logo.style.right = 20; logo.style.bottom = 14; card.Add(logo); body.Add(card);
            var linkLabel = v.Text(v.L("Profil bağlantısı", "Profile link"), 12, false, "mt"); linkLabel.style.marginTop = 16; linkLabel.style.marginBottom = 6; body.Add(linkLabel);
            var url = v.Row(v.Icon("link", 16, "mt"), v.Text(v.Services.Share.ProfileUrl.Replace("https://", ""), 15)); PanelUi.Gap(url, 10); PanelUi.Grow(url); PanelUi.Pad(url, 0, 14); v.Decorate(url, "dp", 14); url.style.height = 48;
            Button copy = null;
            copy = PanelUi.Action(v, v.L("Bağlantıyı kopyala", "Copy link"), () =>
            {
                v.Services.Share.CopyToClipboard(); copy.text = "✓ " + v.L("Kopyalandı", "Copied"); v.Decorate(copy, "#3CCB6E", 14, 4, "#22914A"); copy.style.color = v.C("#fff");
                copy.schedule.Execute(() => { copy.text = v.L("Bağlantıyı kopyala", "Copy link"); v.Decorate(copy, "linear-gradient(var(--a1),var(--a2))", 14, 4, "aS"); copy.style.color = v.C("aT"); }).StartingIn(1800);
            });
            copy.name = "copy-profile-link"; copy.style.width = 180; copy.style.height = 48; copy.style.marginLeft = 10;
            body.Add(v.Row(url, copy));
            var via = v.Text(v.L("Şununla paylaş", "Share via"), 12, false, "mt"); via.style.marginTop = 16; via.style.marginBottom = 6; body.Add(via);
            var targets = v.Row(); targets.name = "share-targets"; targets.style.alignItems = Align.Stretch;
            var names = new[] { "WhatsApp", "Telegram", "X", "Instagram", v.L("Diğer", "More") };
            var glyphs = new[] { "W", "T", "X", "IG", "•••" };
            var colors = new[] { "#25D366", "#2AABEE", "#2A2F3A", "linear-gradient(135deg,#F9A13A,#E1306C 55%,#8A3AB9)", "pnH" };
            for (var i = 0; i < 5; i++)
            {
                var platform = i == 4 ? "More" : names[i]; var name = names[i];
                var button = PanelUi.CustomButton(v, "", () =>
                {
                    v.Services.Share.ShareTo(platform);
                    v.Toast(v.L(name + " açılıyor…", "Opening " + name + "…"));
                }, "g-share-target");
                button.name = "share-to-" + platform.ToLowerInvariant();
                var icon = PanelUi.Badge(v, glyphs[i], colors[i], "#fff", 18); icon.style.width = icon.style.height = 46; icon.style.justifyContent = Justify.Center; icon.style.marginBottom = 8; button.Add(icon); button.Add(v.Text(name, 13, true));
                if (i < 4) button.style.marginRight = 10; targets.Add(button);
            }
            body.Add(targets);
            var inviteText = v.Column(v.Text(v.L("Arkadaşını davet et, çip kazan", "Invite a friend, earn chips"), 15, true), PanelUi.Wrap(v, v.L("Bağlantınla gelen arkadaşın ilk elini oynayınca ikiniz de 2.000 çip kazanırsınız.", "You both get 2,000 chips when they play their first hand with your link."), 12)); PanelUi.Grow(inviteText);
            var reward = v.Row(PanelUi.Coin(v, 16), v.Text("+" + v.N(2000), 15, true, "#FFC94A")); PanelUi.Gap(reward, 6); PanelUi.Pad(reward, 6, 12); v.Decorate(reward, "rgba(255,201,74,.14)", 10);
            var invite = v.Row(inviteText, reward); invite.style.marginTop = 16; PanelUi.Pad(invite, 12, 16); v.Decorate(invite, "sd", 16); body.Add(invite);
            return root;
        }
    }
}
