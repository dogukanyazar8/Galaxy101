using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Galaxy101.Contracts;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    public sealed partial class LobbyScreen
    {
        // Bottom (you), right, top, left inside the 872×592 room area (reference POS).
        private static readonly Vector2[] SeatSpots = { new Vector2(436, 520), new Vector2(800, 308), new Vector2(436, 92), new Vector2(72, 308) };
        private float roomShownAt, countdownAt;
        private int countdownFrom;
        private bool leaving, copied;
        private readonly Dictionary<Guid, string> seen = new Dictionary<Guid, string>();

        /// <summary>"X masaya oturdu" / bot / left notices from consecutive room states.</summary>
        private void NoteSeats(TableStateMessage state)
        {
            var now = state.Table.Seats.Where(s => s?.Player != null).ToDictionary(s => s.Player.Id, s => s.Player.Name);
            if (seen.Count > 0)
                foreach (var joined in now.Where(p => !seen.ContainsKey(p.Key) && p.Key != backend.Me))
                {
                    bool bot = state.Table.Seats.Any(s => s?.Player?.Id == joined.Key && s.Bot);
                    Flash(bot ? L("Bot koltuğa oturdu", "A bot took the seat") : L(joined.Value + " masaya oturdu", joined.Value + " sat down"), "info");
                    if (invite != null && invite.Sent.TryGetValue(joined.Key, out var sent)) sent.State = "in";
                }
            seen.Clear(); foreach (var p in now) seen[p.Key] = p.Value;
        }

        public int Countdown => countdownFrom <= 0 ? 0 : Mathf.Max(0, Mathf.CeilToInt(countdownFrom - (Time.realtimeSinceStartup - countdownAt)));

        private void BuildRoom()
        {
            var room = backend.Room; var t = room.Table; int mine = Mathf.Clamp(room.YourSeat, 0, 3);
            bool host = t.HostId == backend.Me, pairs = t.Type == "pairs";
            int seated = t.Seats.Count(s => s != null); bool full = seated == 4;
            var page = Fill("room"); page.pickingMode = PickingMode.Ignore; body.Add(page);
            if (Time.realtimeSinceStartup - roomShownAt < .3f) { float began = roomShownAt; page.style.opacity = 0; Animate(() => page.style.opacity = Mathf.Clamp01((Time.realtimeSinceStartup - began) / .3f)); }

            // Top bar: Ayrıl, BEKLEME ODASI (+ Oda sahibi sensin) and the name, Davet et, code box (private), balance.
            var bar = Row(); MenuView.Place(bar, 0, 0, 1280, 86); Pad(bar, 0, 28, 0, 28); page.Add(bar);
            var leave = Row(); leave.name = "room-leave"; leave.style.height = 46; Pad(leave, 0, 16, 0, 12); Raise(leave, "var(--pnH)", 14, 4, "var(--pnS)");
            var li = view.Icon("leave", 20, "var(--ic)"); li.style.marginRight = 8; leave.Add(li); leave.Add(Text(L("Ayrıl", "Leave"), 15, "Montserrat-700", "#F4F1E8"));
            GalaxyFilters.HoverBrightness(leave, 1.25f); Tap(leave, () => _ = Leave()); bar.Add(leave);
            var titles = Box(); titles.style.marginLeft = 14; titles.style.flexShrink = 1;
            var tag = Row(); var cap = Text(L("BEKLEME ODASI", "WAITING ROOM"), 11, "Baloo2-800", "var(--a1)"); MenuView.Tracking(cap, 1.8f, 11); tag.Add(cap);
            if (host) { var own = Text(L("Oda sahibi sensin", "You are the host"), 11, "Baloo2-800", "var(--a1)"); own.name = "room-host"; own.style.marginLeft = 10; Pad(own, 2, 8, 2, 8); view.Decorate(own, "rgba(255,201,74,.16)", 7); tag.Add(own); }
            titles.Add(tag);
            var name = Text(t.Name, 24, "Montserrat-700", "#F4F1E8"); name.style.marginTop = 3; name.style.maxWidth = 520; name.style.overflow = Overflow.Hidden; name.style.textOverflow = TextOverflow.Ellipsis; titles.Add(name);
            bar.Add(titles); bar.Add(Grow());
            int counting = Countdown;
            if (!full && counting == 0)
            {
                var inv = Row(); inv.name = "room-invite"; inv.style.height = 46; inv.style.marginLeft = 14; Pad(inv, 0, 18, 0, 14); Raise(inv, "linear-gradient(var(--a1),var(--a2))", 14, 4, "var(--aS)");
                var ii = view.Icon("invite", 16, "var(--aT)"); ii.style.marginRight = 8; inv.Add(ii); inv.Add(Text(L("Davet et", "Invite"), 15, "Montserrat-800", "var(--aT)"));
                int pending = invite?.Sent.Values.Count(x => x.State == "pend") ?? 0;
                if (pending > 0) { var n = Text(pending.ToString(), 11, "Baloo2-800", "var(--a1)"); n.style.marginLeft = 8; n.style.minWidth = 20; n.style.height = 20; n.style.unityTextAlign = TextAnchor.MiddleCenter; view.Decorate(n, "var(--aT)", 10); inv.Add(n); }
                GalaxyFilters.HoverBrightness(inv, 1.08f); Tap(inv, () => OpenInvite(null)); bar.Add(inv);
            }
            if (t.Private && room.Code != null)
            {
                var codeBox = Row(); codeBox.style.height = 46; codeBox.style.marginLeft = 14; Pad(codeBox, 0, 6, 0, 16); view.Decorate(codeBox, "rgba(0,0,0,.2)", 14); DashedBorder(codeBox, 1, view.C("var(--bd)"));
                codeBox.Add(Text(L("Kod", "Code"), 12, "Baloo2-800", "var(--mt)"));
                var code = Text(room.Code, 18, "Montserrat-700", "var(--a1)"); code.name = "room-code"; code.style.marginLeft = 10; MenuView.Tracking(code, 3, 18); codeBox.Add(code);
                var copy = Text(copied ? L("Kopyalandı", "Copied") : L("Kodu kopyala", "Copy code"), 13, "Montserrat-700", "#F4F1E8"); copy.name = "room-copy"; copy.style.marginLeft = 10; copy.style.height = 34; copy.style.unityTextAlign = TextAnchor.MiddleCenter; Pad(copy, 0, 12, 0, 12); Raise(copy, "var(--pnH)", 10, 3, "var(--pnS)");
                Tap(copy, CopyCode); codeBox.Add(copy);
                var share = Box("room-share"); Square(share, 34); share.style.marginLeft = 6; Raise(share, "var(--pnH)", 10, 3, "var(--pnS)"); share.Add(view.Icon("share", 16, "var(--ic)")); share.tooltip = L("Paylaş", "Share");
                Tap(share, () => OpenDialog(new Dialog { Kind = "share" })); codeBox.Add(share);
                bar.Add(codeBox);
            }
            bar.Add(Balance(backend.Chips, false));

            // The oval table with pool, status and four seat dots; seats around it.
            var area = Box(); MenuView.Place(area, 28, 100, 872, 592); page.Add(area);
            var oval = Box("room-table"); MenuView.Place(oval, 156, 176, 560, 264);
            view.Decorate(oval, "radial-gradient(ellipse at 50% 40%,var(--pnH),var(--sd) 75%)", 132);
            Border(oval, 12, view.C("var(--dp)")); GalaxyFilters.Shadow(oval, 0, 24, 25, new Color(0, 0, 0, .45f)); area.Add(oval);
            var rim = Box(); rim.style.position = Position.Absolute; rim.style.left = rim.style.top = rim.style.right = rim.style.bottom = -2; Radius(rim, 122); Border(rim, 2, view.C("var(--a2)")); GalaxyFilters.Shadow(rim, 0, 0, 30, view.C("var(--gl)")); oval.Add(rim);
            var middle = Box(); MenuView.Place(middle, 156, 176, 560, 264); middle.style.alignItems = Align.Center; middle.style.justifyContent = Justify.Center; area.Add(middle);
            var brand = Text("GALAXY 101", 22, "Montserrat-800", "var(--a1)"); MenuView.Tracking(brand, 4, 22); middle.Add(brand);
            var pool = Row(); pool.style.marginTop = 8; Pad(pool, 6, 14, 6, 14); view.Decorate(pool, "rgba(0,0,0,.28)", 999);
            var coin = Box(); Square(coin, 18); view.Decorate(coin, "radial-gradient(circle at 35% 30%,#FFE9A8,#F5A623 60%,#B86E0F)", 9); pool.Add(coin);
            var pl = Text(L("Toplam havuz", "Total pool"), 13, "Baloo2-800", "var(--mt)"); pl.style.marginLeft = 8; pool.Add(pl);
            var pv = Text(view.N(t.Stake * 4), 17, "Montserrat-700", "#F4F1E8"); pv.style.marginLeft = 8; pool.Add(pv); middle.Add(pool);
            var status = Text(StatusLine(full, seated, counting), 14, "Baloo2-800", full ? "#8EF0B0" : "var(--ic)"); status.name = "room-status"; status.style.marginTop = 8; middle.Add(status);
            var dots = Row(); dots.style.marginTop = 8;
            for (int s = 0; s < 4; s++) { var d = Box(); Square(d, 10); if (s > 0) d.style.marginLeft = 6; view.Decorate(d, t.Seats[s] != null ? "var(--a1)" : "rgba(255,255,255,.15)", 5); dots.Add(d); }
            middle.Add(dots);
            if (counting > 0) Animate(() => { int left = Countdown; status.text = StatusLine(full, seated, left); });

            for (int s = 0; s < 4; s++) area.Add(Seat(t, s, (s - mine + 4) % 4, host, pairs, counting > 0));

            // Right panel: ÖZET facts, badges, prize note, hint, Başlat.
            var panel = Box("room-summary"); panel.style.position = Position.Absolute; panel.style.right = 28; panel.style.top = 100; panel.style.bottom = 28; panel.style.width = 330;
            Pad(panel, 20, 20, 20, 20); Raise(panel, "var(--pn)", 24, 8, "var(--pnS)"); Border(panel, 1, new Color(1, 1, 1, .08f)); page.Add(panel);
            var sc = Text(L("ÖZET", "SUMMARY"), 11, "Baloo2-800", "var(--mt)"); MenuView.Tracking(sc, 1.8f, 11); panel.Add(sc);
            panel.Add(Facts("var(--sd)", (L("BAHİS", "BET"), view.N(t.Stake)), (L("HAVUZ", "POOL"), view.N(t.Stake * 4)), (L("EL", "HANDS"), t.Hands.ToString()), (L("TUR SÜRESİ", "TURN TIME"), L(t.TurnSeconds + " sn", t.TurnSeconds + "s"))));
            var badges = Row(); badges.style.flexWrap = Wrap.Wrap; badges.style.marginTop = 6; badges.style.flexShrink = 1;
            foreach (var b in Badges(t, true)) badges.Add(b);
            panel.Add(badges);
            var prize = Row(); prize.style.marginTop = 8; Pad(prize, 12, 14, 12, 14); view.Decorate(prize, "rgba(255,201,74,.1)", 14); Border(prize, 1, new Color(1, .79f, .29f, .22f));
            prize.Add(view.Icon("trophy", 22, "var(--a1)"));
            var pt = Text(pairs ? L("Kazanan takım " + view.N(t.Stake * 4) + " çipi alır; kişi başı " + view.N(t.Stake * 2) + ".", "The winning team takes " + view.N(t.Stake * 4) + " chips; " + view.N(t.Stake * 2) + " each.") : L("Kazanan " + view.N(t.Stake * 4) + " çipin tamamını alır.", "The winner takes all " + view.N(t.Stake * 4) + " chips."), 13, "Baloo2-700", "var(--ic)");
            pt.style.whiteSpace = WhiteSpace.Normal; pt.style.flexShrink = 1; pt.style.marginLeft = 10; prize.Add(pt); panel.Add(prize);
            panel.Add(Grow());
            var hint = Text(host ? full ? L("Herkes yerinde, oyunu başlat.", "Everyone is seated, start the game.") : L("Dört koltuk dolunca başlatabilirsin. Boş koltuğa bot ekleyebilirsin.", "You can start once all four seats are taken. Add bots to empty seats.") : L("Oda sahibi oyunu başlatınca masa açılır.", "The table opens when the host starts the game."), 13, "Baloo2-700", "var(--mt)");
            hint.style.whiteSpace = WhiteSpace.Normal; hint.style.unityTextAlign = TextAnchor.MiddleCenter; hint.style.marginBottom = 14; panel.Add(hint);
            bool ready = host && full && counting == 0;
            string label = counting > 0 ? L("Oyun başlıyor · " + counting, "Starting · " + counting) : host ? full ? L("Başlat", "Start") : L("Başlat · " + seated + "/4", "Start · " + seated + "/4") : L("Oda sahibi bekleniyor", "Waiting for the host");
            var start = Row(); start.name = "room-start"; start.style.height = 62; start.style.justifyContent = Justify.Center;
            Raise(start, ready ? "linear-gradient(#5BE38A,#2BB45F)" : "var(--pnH)", 18, 5, ready ? "#18793F" : "var(--pnS)");
            if (!host || counting > 0) { var spin = Spinner(18, ready ? "#05301A" : "var(--mt)"); spin.style.marginRight = 10; start.Add(spin); }
            var sl = Text(label, 20, "Montserrat-800", ready ? "#05301A" : "var(--mt)"); start.Add(sl);
            if (counting > 0) Animate(() => { int left = Countdown; sl.text = left > 0 ? L("Oyun başlıyor · " + left, "Starting · " + left) : L("Masa açılıyor…", "Opening the table…"); });
            if (ready) { Pulse(start); Tap(start, () => _ = Start()); }
            panel.Add(start);

            if (InviteOpen) BuildInvite(t, host);
        }

        private string StatusLine(bool full, int seated, int counting) =>
            counting > 0 ? L("Oyun başlıyor · " + counting, "Starting · " + counting) : full ? L("Masa hazır", "Table is ready") : L("Oyuncular bekleniyor · " + seated + "/4", "Waiting for players · " + seated + "/4");

        private VisualElement Seat(TableSummary t, int seat, int spot, bool host, bool pairs, bool counting)
        {
            var at = SeatSpots[spot];
            var slot = Box("room-seat-" + seat); MenuView.Place(slot, at.x - 100, at.y - 60, 200, 150); slot.style.alignItems = Align.Center;
            var entry = t.Seats[seat];
            if (entry?.Player != null)
            {
                bool me = entry.Player.Id == backend.Me;
                var holder = Box(); Square(holder, 76);
                var face = Box(); Square(face, 76); view.Decorate(face, entry.Bot ? "#5B6390" : entry.Player.Color ?? "#3D7BFF", 38); Border(face, 3, view.C(me ? "var(--a1)" : "rgba(255,255,255,.85)"));
                GalaxyFilters.Shadow(face, 0, 10, 12, new Color(0, 0, 0, .4f));
                face.Add(Text(Initials(entry.Player.Name), 24, "Montserrat-700", "#fff")); holder.Add(face);
                if (entry.Player.Id == t.HostId) { var crown = view.Icon("crown", 26, "#FFD964"); crown.name = "room-crown"; crown.style.position = Position.Absolute; crown.style.top = -18; crown.style.left = 25; crown.tooltip = L("Oda sahibi", "Host"); holder.Add(crown); }
                if (host && !me && !counting)
                {
                    var kick = Box("room-kick-" + seat); Square(kick, 26); kick.style.position = Position.Absolute; kick.style.right = -6; kick.style.top = -4; Raise(kick, "#FF5E6C", 13, 3, "#C23E4A");
                    kick.Add(view.Icon("close", 11, "#fff")); kick.tooltip = L("Masadan çıkar", "Remove from table"); string who = entry.Player.Name;
                    GalaxyFilters.HoverBrightness(kick, 1.1f); Tap(kick, () => _ = Kick(seat, who, entry.Bot)); holder.Add(kick);
                }
                string tagText = entry.Bot ? "BOT" : me ? L("SEN", "YOU") : L("SV ", "LV ") + Math.Max(1, entry.Player.Level);
                var tag = Text(tagText, 10, "Baloo2-800", entry.Bot ? "#fff" : me ? "var(--aT)" : "var(--ic)"); Pad(tag, 1, 8, 1, 8); view.Decorate(tag, entry.Bot ? "#5B6390" : me ? "var(--a1)" : "var(--pnH)", 7);
                tag.style.position = Position.Absolute; tag.style.bottom = -8; tag.style.alignSelf = Align.Center; holder.Add(tag);
                slot.Add(holder);
                var label = Text(entry.Player.Name, 16, "Montserrat-700", "#F4F1E8"); label.style.marginTop = 12; slot.Add(label);
                if (pairs) { bool a = seat % 2 == 0; var team = Text(a ? L("Takım A", "Team A") : L("Takım B", "Team B"), 11, "Baloo2-800", "#fff"); team.style.marginTop = 6; Pad(team, 2, 9, 2, 9); view.Decorate(team, a ? "#3D7BFF" : "#FF6B6B", 7); slot.Add(team); }
                return slot;
            }
            var empty = Box(); Square(empty, 76); view.Decorate(empty, "rgba(0,0,0,.18)", 38); DashedBorder(empty, 2, view.C("var(--bd)"));
            var dots = Row();
            for (int i = 0; i < 3; i++)
            {
                var d = Box(); Square(d, 6); if (i > 0) d.style.marginLeft = 4; view.Decorate(d, "var(--mt)", 3); dots.Add(d); float phase = i * .2f;
                Animate(() => d.style.opacity = .25f + .75f * (0.5f - 0.5f * Mathf.Cos((Time.realtimeSinceStartup - phase) / 1.2f * Mathf.PI * 2)));
            }
            empty.Add(dots); slot.Add(empty);
            var el = Text(L("Boş koltuk", "Empty seat"), 14, "Baloo2-700", "var(--mt)"); el.style.marginTop = 6; slot.Add(el);
            if (!counting)
            {
                var buttons = Row(); buttons.style.marginTop = 6;
                var inv = Row(); inv.name = "room-invite-" + seat; Pad(inv, 7, 12, 7, 12); Raise(inv, "linear-gradient(var(--a1),var(--a2))", 11, 3, "var(--aS)");
                var ii = view.Icon("invite", 14, "var(--aT)"); ii.style.marginRight = 6; inv.Add(ii); inv.Add(Text(L("Davet", "Invite"), 13, "Montserrat-800", "var(--aT)"));
                GalaxyFilters.HoverBrightness(inv, 1.08f); Tap(inv, () => OpenInvite(seat)); buttons.Add(inv);
                if (host)
                {
                    var bot = Row(); bot.name = "room-bot-" + seat; bot.style.marginLeft = 6; Pad(bot, 7, 13, 7, 13); Raise(bot, "var(--pnH)", 11, 3, "var(--pnS)");
                    var bi = view.Icon("bot", 15, "var(--ic)"); bi.style.marginRight = 6; bot.Add(bi); bot.Add(Text(L("Bot ekle", "Add bot"), 13, "Montserrat-700", "#F4F1E8"));
                    GalaxyFilters.HoverBrightness(bot, 1.2f); Tap(bot, () => _ = AddBot(seat)); buttons.Add(bot);
                }
                slot.Add(buttons);
            }
            return slot;
        }

        private VisualElement Spinner(int size, string color)
        {
            var ring = Box(); Square(ring, size); Radius(ring, size / 2f); Border(ring, 3, view.C(color)); ring.style.borderTopColor = Color.clear;
            Animate(() => ring.style.rotate = new Rotate(Mathf.Repeat(Time.realtimeSinceStartup / .8f, 1) * 360)); return ring;
        }

        private void CopyCode()
        {
            var code = backend.Room?.Code; if (code == null) return;
            view.Copy(code); copied = true; Render();
            layer?.schedule.Execute(() => { copied = false; if (screen == "wait") Render(); }).ExecuteLater(1600);
        }

        private async System.Threading.Tasks.Task Leave()
        {
            leaving = true; invite = null; inviteOpen = false;
            string error = await backend.LeaveAsync();
            if (layer == null) return;
            if (error != null && error != ErrorCodes.NotFound) { leaving = false; Flash(L("Ayrılamadın (" + error + ")", "Could not leave (" + error + ")"), "bad"); return; }
            if (backend.Room == null || error == ErrorCodes.NotFound) { screen = "list"; animateCards = true; Render(); }
            Flash(L("Bekleme odasından ayrıldın", "You left the waiting room"), "info");
        }

        private async System.Threading.Tasks.Task AddBot(int seat)
        {
            string error = await backend.AddBotAsync(seat);
            if (error != null && layer != null) Flash(L("Bot eklenemedi (" + error + ")", "Could not add a bot (" + error + ")"), "bad");
        }

        private async System.Threading.Tasks.Task Kick(int seat, string who, bool bot)
        {
            string error = await backend.KickAsync(seat);
            if (layer == null) return;
            if (error != null) Flash(L("Çıkarılamadı (" + error + ")", "Could not remove (" + error + ")"), "bad");
            else Flash(bot ? L("Bot masadan çıkarıldı", "The bot was removed") : L(who + " masadan çıkarıldı", who + " was removed"), "bad");
        }

        private async System.Threading.Tasks.Task Start()
        {
            string error = await backend.StartAsync();
            if (error != null && layer != null) Flash(error == TableErrors.NotReady ? L("Dört koltuk dolmadan başlatılamaz", "All four seats must be taken") : L("Başlatılamadı (" + error + ")", "Could not start (" + error + ")"), "bad");
        }

        // ---------- invite (S14, S15) ----------
        public sealed class InviteState
        {
            public string Tab = "friends", Query = "";
            public int? Seat;
            public float OpenedAt;
            public List<FriendResponse> Friends, Recent;
            public readonly Dictionary<Guid, Sent> Sent = new Dictionary<Guid, Sent>();
        }

        public sealed class Sent { public string State = "pend"; public float At; }

        private InviteState invite;
        public InviteState Invite => invite;
        public const float InviteSeconds = 30;

        public void OpenInvite(int? seat)
        {
            invite = invite ?? new InviteState(); inviteOpen = true;
            invite.Seat = seat; invite.Query = ""; invite.OpenedAt = Time.realtimeSinceStartup;
            Render();
            _ = LoadInvitees();
        }

        private async System.Threading.Tasks.Task LoadInvitees()
        {
            var state = invite; if (state == null) return;
            var friends = await backend.FriendsAsync(); var recent = await backend.RecentAsync();
            if (invite != state) return;
            state.Friends = friends; state.Recent = recent.Where(r => friends.All(f => f.Player?.Id != r.Player?.Id)).ToList();
            if (screen == "wait") Render();
        }

        private bool inviteOpen;
        public bool InviteOpen => inviteOpen && invite != null;
        /// <summary>Closing keeps the sent invites (their countdowns and the pending badge on Davet et).</summary>
        private void CloseInvite() { inviteOpen = false; Render(); }

        private void BuildInvite(TableSummary t, bool host)
        {
            var state = invite; int free = t.Seats.Count(s => s == null);
            var shade = Fill("invite-backdrop"); shade.pickingMode = PickingMode.Position; view.Decorate(shade, "rgba(0,0,0,.55)"); Tap(shade, CloseInvite); body.Add(shade);
            var modal = Row(); modal.name = "invite-modal"; modal.pickingMode = PickingMode.Position; modal.style.alignItems = Align.Stretch; MenuView.Place(modal, 190, 70, 900, 580); modal.style.overflow = Overflow.Hidden;
            Raise(modal, "var(--pn)", 28, 10, "var(--pnS)"); Border(modal, 1, new Color(1, 1, 1, .1f)); GalaxyFilters.Shadow(modal, 0, 40, 45, new Color(0, 0, 0, .6f));
            modal.RegisterCallback<ClickEvent>(e => e.StopPropagation()); Pop(modal, state.OpenedAt, .32f); body.Add(modal);

            var left = Box(); left.style.flexGrow = 1; left.style.flexBasis = 0; Pad(left, 22, 22, 0, 28); modal.Add(left);
            var head = Row();
            var icon = Box(); Square(icon, 50); Raise(icon, "linear-gradient(160deg,var(--a1),var(--a2))", 16, 4, "var(--aS)"); icon.Add(view.Icon("invite", 24, "var(--aT)")); head.Add(icon);
            var words = Box(); words.style.marginLeft = 14;
            words.Add(Text(L("Arkadaşlarını davet et", "Invite friends"), 26, "Montserrat-700", "#F4F1E8"));
            var sub = Text(free > 0 ? L(free + " boş koltuk · davet 30 sn geçerli", free + " empty " + (free > 1 ? "seats" : "seat") + " · invites last 30 s") : L("Masa dolu · davet gönderilemez", "Table is full · no invites"), 13, "Baloo2-700", "var(--mt)"); sub.style.marginTop = 3; words.Add(sub);
            head.Add(words); left.Add(head);

            var tools = Row(); tools.style.marginTop = 14;
            var tabs = Segment();
            int online = state.Friends?.Count(f => f.Presence != "offline") ?? 0;
            foreach (var (value, label, n) in new[] { ("friends", L("Arkadaşlar", "Friends"), state.Friends == null ? "…" : online + "/" + state.Friends.Count), ("recent", L("Son oynadıkların", "Recent players"), state.Recent == null ? "…" : state.Recent.Count.ToString()) })
            {
                string pick = value; bool on = state.Tab == value;
                var tab = SegmentOption("invite-tab-" + value, label, on, () => { state.Tab = pick; Render(); }, 14, 9, 14, value == "friends" ? 4 : 0);
                var count = Text(n, 11, "Baloo2-800", on ? "var(--aT)" : "var(--mt)"); count.style.marginLeft = 7; count.style.opacity = .75f; tab.Add(count); tabs.Add(tab);
            }
            tools.Add(tabs);
            var search = Row(); search.style.flexGrow = 1; search.style.height = 44; search.style.marginLeft = 10; Pad(search, 0, 14, 0, 14); view.Decorate(search, "var(--dp)", 14);
            search.Add(view.Icon("search", 16, "var(--mt)"));
            var field = Input("invite-search", state.Query, L("Oyuncu ara", "Search players"), 14, "Baloo2-700", "#F4F1E8", 24); field.style.flexGrow = 1; field.style.marginLeft = 8;
            search.Add(field); tools.Add(search); left.Add(tools);

            var list = view.Scroll(); list.name = "invite-list"; list.style.flexGrow = 1; list.style.marginTop = 14; list.contentContainer.style.paddingRight = 6; list.contentContainer.style.paddingBottom = 20; left.Add(list);
            void FillRows()
            {
                list.Clear();
                var pool = state.Tab == "recent" ? state.Recent : state.Friends;
                if (pool == null) { var wait = Spinner(26, "var(--mt)"); wait.style.alignSelf = Align.Center; wait.style.marginTop = 40; list.Add(wait); return; }
                string q = state.Query.Trim().ToLower(new System.Globalization.CultureInfo("tr-TR"));
                int Order(FriendResponse f) => f.Presence == "online" ? 0 : f.Presence == "playing" ? 1 : 2;
                var rows = pool.Where(f => f.Player != null && (q.Length == 0 || f.Player.Name.ToLower(new System.Globalization.CultureInfo("tr-TR")).Contains(q))).OrderBy(Order).ThenBy(f => f.Player.Name).ToList();
                if (rows.Count == 0) { var none = Text(pool.Count == 0 ? state.Tab == "recent" ? L("Henüz birlikte oynadığın kimse yok", "Nobody played with you yet") : L("Henüz arkadaşın yok", "No friends yet") : L("Bu isimde oyuncu yok", "No player with that name"), 14, "Baloo2-700", "var(--mt)"); none.style.alignSelf = Align.Center; none.style.marginTop = 40; list.Add(none); return; }
                foreach (var f in rows) list.Add(InviteRow(f, t, free));
            }
            FillRows();
            field.RegisterValueChangedCallback(e => { state.Query = e.newValue ?? ""; FillRows(); });

            // Right: seats, counts, code, notes.
            var right = Box(); right.style.width = 290; Pad(right, 22, 22, 22, 22); view.Decorate(right, "var(--sd)", 0); right.style.borderLeftWidth = 1; right.style.borderLeftColor = new Color(1, 1, 1, .06f); modal.Add(right);
            var top = Row(); top.style.justifyContent = Justify.FlexEnd;
            var close = Box("invite-close"); Square(close, 40); Raise(close, "var(--pnH)", 12, 3, "var(--pnS)"); close.Add(view.Icon("close", 14, "var(--ic)")); close.tooltip = L("Kapat", "Close"); GalaxyFilters.HoverBrightness(close, 1.2f); Tap(close, CloseInvite); top.Add(close); right.Add(top);
            var seatsCap = Text(L("KOLTUKLAR", "SEATS"), 11, "Baloo2-800", "var(--mt)"); MenuView.Tracking(seatsCap, 1.8f, 11); seatsCap.style.marginTop = 14; right.Add(seatsCap);
            var seats = Row(); seats.style.marginTop = 14; seats.style.justifyContent = Justify.SpaceBetween;
            for (int s = 0; s < 4; s++)
            {
                var e = t.Seats[s]; var chip = Box(); Square(chip, 56);
                if (e?.Player != null) { view.Decorate(chip, e.Bot ? "#5B6390" : e.Player.Color ?? "#3D7BFF", 28); Border(chip, 2, view.C(e.Player.Id == backend.Me ? "var(--a1)" : "rgba(255,255,255,.75)")); chip.Add(Text(Initials(e.Player.Name), 14, "Montserrat-700", "#fff")); chip.tooltip = e.Player.Name; }
                else { bool chosen = state.Seat == s; view.Decorate(chip, chosen ? "rgba(255,201,74,.16)" : "rgba(0,0,0,.2)", 28); DashedBorder(chip, 2, view.C(chosen ? "var(--a1)" : "var(--bd)")); chip.Add(Text("+", 14, "Montserrat-700", chosen ? "var(--a1)" : "var(--mt)")); }
                seats.Add(chip);
            }
            right.Add(seats);
            int pending = state.Sent.Values.Count(x => x.State == "pend");
            var line = Text(L((4 - free) + "/4 dolu" + (pending > 0 ? " · " + pending + " davet bekliyor" : ""), (4 - free) + "/4 taken" + (pending > 0 ? " · " + pending + " pending" : "")), 13, "Baloo2-700", "var(--ic)"); line.name = "invite-seat-line"; line.style.marginTop = 14; right.Add(line);
            var rule = Box(); rule.style.height = 1; rule.style.marginTop = 14; rule.style.marginBottom = 14; rule.style.backgroundColor = new Color(1, 1, 1, .07f); right.Add(rule);
            var room = backend.Room;
            if (t.Private && room?.Code != null)
            {
                var cc = Text(L("KODLA DAVET", "INVITE BY CODE"), 11, "Baloo2-800", "var(--mt)"); MenuView.Tracking(cc, 1.8f, 11); right.Add(cc);
                var codeBox = Box(); codeBox.style.alignItems = Align.Center; codeBox.style.marginTop = 10; Pad(codeBox, 14, 0, 14, 0); view.Decorate(codeBox, "rgba(0,0,0,.18)", 18); DashedBorder(codeBox, 2, view.C("var(--bd)"));
                var code = Text(room.Code, 30, "Montserrat-800", "var(--a1)"); MenuView.Tracking(code, 5, 30); codeBox.Add(code); right.Add(codeBox);
                var buttons = Row(); buttons.style.marginTop = 10;
                var copy = Text(copied ? L("Kopyalandı", "Copied") : L("Kodu kopyala", "Copy code"), 14, "Montserrat-700", "#F4F1E8"); copy.name = "invite-copy"; copy.style.flexGrow = 1; copy.style.height = 42; copy.style.unityTextAlign = TextAnchor.MiddleCenter; Raise(copy, "var(--pnH)", 13, 4, "var(--pnS)"); Tap(copy, CopyCode); buttons.Add(copy);
                var share = Text(L("Paylaş", "Share"), 14, "Montserrat-700", "#fff"); share.style.flexGrow = 1; share.style.marginLeft = 8; share.style.height = 42; share.style.unityTextAlign = TextAnchor.MiddleCenter; Raise(share, "#3D7BFF", 13, 4, "#2148B0"); Tap(share, () => OpenDialog(new Dialog { Kind = "share" })); buttons.Add(share);
                right.Add(buttons);
            }
            else
            {
                var note = Row(); note.style.alignItems = Align.FlexStart; Pad(note, 12, 14, 12, 14); view.Decorate(note, "rgba(255,255,255,.04)", 14);
                note.Add(view.Icon("info", 18, "var(--mt)"));
                var nt = Text(L("Açık masa lobide listelenir; davet edilen arkadaşın doğrudan boş koltuğa oturur.", "Open tables are listed in the lobby; invited friends sit straight into an empty seat."), 13, "Baloo2-700", "var(--mt)");
                nt.style.whiteSpace = WhiteSpace.Normal; nt.style.flexShrink = 1; nt.style.marginLeft = 10; note.Add(nt); right.Add(note);
            }
            right.Add(Grow());
            var foot = Text(L("Kabul eden arkadaşın boş koltuğa otomatik oturur.", "Friends who accept take an empty seat automatically."), 12, "Baloo2-700", "var(--mt)"); foot.style.whiteSpace = WhiteSpace.Normal; foot.style.unityTextAlign = TextAnchor.MiddleCenter; right.Add(foot);
        }

        private VisualElement InviteRow(FriendResponse f, TableSummary t, int free)
        {
            var p = f.Player; bool seated = t.Seats.Any(s => s?.Player?.Id == p.Id);
            invite.Sent.TryGetValue(p.Id, out var sent);
            string st = seated ? "in" : sent?.State;
            float left = sent != null && st == "pend" ? InviteSeconds - (Time.realtimeSinceStartup - sent.At) : 0;
            if (st == "pend" && left <= 0) { sent.State = st = "exp"; }
            var row = Row(); row.name = "invite-row-" + p.Id; row.style.marginBottom = 8; Pad(row, 10, 12, 10, 10); view.Decorate(row, "var(--sd)", 16);
            if (st == "pend") Border(row, 2, new Color(1, .79f, .29f, .45f)); else if (st == "in") Border(row, 2, new Color(.235f, .8f, .43f, .4f));
            row.style.opacity = f.Presence == "offline" && st != "in" ? .55f : 1;
            var face = Box(); Square(face, 46); view.Decorate(face, p.Color ?? "#3D7BFF", 23); Border(face, 2, new Color(1, 1, 1, .8f)); face.Add(Text(Initials(p.Name), 16, "Montserrat-700", "#fff"));
            var dot = Box(); Square(dot, 14); dot.style.position = Position.Absolute; dot.style.right = -1; dot.style.bottom = -1; view.Decorate(dot, f.Presence == "online" ? "#3CCB6E" : f.Presence == "playing" ? "#FFC94A" : "#6B7393", 7); Border(dot, 3, view.C("var(--sd)")); face.Add(dot);
            row.Add(face);
            var words = Box(); words.style.flexGrow = 1; words.style.flexShrink = 1; words.style.marginLeft = 14;
            var nameLine = Row(); var name = Text(p.Name, 17, "Montserrat-700", "#F4F1E8"); name.style.flexShrink = 1; name.style.overflow = Overflow.Hidden; name.style.textOverflow = TextOverflow.Ellipsis; nameLine.Add(name);
            var lv = Text(L("SV ", "LV ") + Math.Max(1, p.Level), 10, "Baloo2-800", "var(--ic)"); lv.style.marginLeft = 8; Pad(lv, 1, 7, 1, 7); view.Decorate(lv, "var(--pnH)", 6); nameLine.Add(lv); words.Add(nameLine);
            string subText = f.Presence == "online" ? L("Çevrimiçi", "Online") : f.Presence == "playing" ? L("Oyunda", "In a game") : f.LastSeen is DateTimeOffset seenAt ? L("Son görülme " + Ago(seenAt), "Last seen " + Ago(seenAt)) : L("Çevrimdışı", "Offline");
            words.Add(Text(subText, 12, "Baloo2-700", f.Presence == "online" ? "#8EF0B0" : f.Presence == "playing" ? "var(--a1)" : "var(--mt)")); row.Add(words);

            string label, bg, fg, shade; bool can = false;
            if (st == "in") { label = L("Masada", "Seated"); bg = "#3CCB6E"; fg = "#062B14"; shade = "#22914A"; }
            else if (st == "pend") { label = L("Bekleniyor " + Mathf.CeilToInt(left), "Waiting " + Mathf.CeilToInt(left)); bg = "var(--pnH)"; fg = "#F4F1E8"; shade = "var(--pnS)"; can = true; }
            else if (free == 0) { label = L("Masa dolu", "Table full"); bg = "var(--dp)"; fg = "var(--mt)"; shade = "rgba(0,0,0,.25)"; }
            else if (f.Presence == "playing") { label = L("Oyunda", "In a game"); bg = "var(--dp)"; fg = "var(--mt)"; shade = "rgba(0,0,0,.25)"; }
            else if (f.Presence == "offline") { label = L("Çevrimdışı", "Offline"); bg = "var(--dp)"; fg = "var(--mt)"; shade = "rgba(0,0,0,.25)"; }
            else if (st == "exp") { label = L("Tekrar davet", "Invite again"); bg = "var(--pnH)"; fg = "#F4F1E8"; shade = "var(--pnS)"; can = true; }
            else if (st == "no") { label = L("Reddetti", "Declined"); bg = "var(--dp)"; fg = "#FF9AA3"; shade = "rgba(0,0,0,.25)"; }
            else { label = L("Davet et", "Invite"); bg = "linear-gradient(var(--a1),var(--a2))"; fg = "var(--aT)"; shade = "var(--aS)"; can = true; }
            var button = Row(); button.name = "invite-send-" + p.Id; button.style.minWidth = 136; button.style.height = 40; button.style.justifyContent = Justify.Center; Pad(button, 0, 14, 0, 14); button.style.overflow = Overflow.Hidden;
            Raise(button, bg, 12, 4, shade);
            if (st == "pend")
            {
                var spin = Spinner(14, fg); spin.style.marginRight = 8; button.Add(spin);
                var bar = Box(); bar.style.position = Position.Absolute; bar.style.left = 0; bar.style.bottom = 0; bar.style.height = 3; bar.style.backgroundColor = view.C("var(--a1)"); button.Add(bar);
                button.tooltip = L("İptal etmek için tıkla", "Click to cancel");
                var text = Text(label, 14, "Montserrat-800", fg); button.Add(text); var s = sent;
                Animate(() =>
                {
                    float remaining = InviteSeconds - (Time.realtimeSinceStartup - s.At);
                    bar.style.width = Length.Percent(Mathf.Clamp01(remaining / InviteSeconds) * 100);
                    if (remaining <= 0 && s.State == "pend") { s.State = "exp"; Flash(L(p.Name + " yanıt vermedi", p.Name + " didn’t respond"), "info"); Render(); return; }
                    text.text = L("Bekleniyor " + Mathf.CeilToInt(Mathf.Max(0, remaining)), "Waiting " + Mathf.CeilToInt(Mathf.Max(0, remaining)));
                });
            }
            else
            {
                if (st == "in") { var tick = view.Icon("tick", 15, fg); tick.style.marginRight = 8; button.Add(tick); }
                button.Add(Text(label, 14, "Montserrat-800", fg));
            }
            if (can) Tap(button, () => _ = SendInvite(p, st == "pend"));
            row.Add(button);
            return row;
        }

        private string Ago(DateTimeOffset at)
        {
            var span = DateTimeOffset.UtcNow - at;
            if (span.TotalMinutes < 60) return L(Math.Max(1, (int)span.TotalMinutes) + " dk önce", Math.Max(1, (int)span.TotalMinutes) + " min ago");
            if (span.TotalHours < 24) return L((int)span.TotalHours + " sa önce", (int)span.TotalHours + " h ago");
            return L((int)span.TotalDays + " gün önce", (int)span.TotalDays + " d ago");
        }

        private async System.Threading.Tasks.Task SendInvite(PlayerSummary p, bool cancel)
        {
            var state = invite; if (state == null) return;
            if (cancel) { state.Sent.Remove(p.Id); Render(); return; }
            var code = backend.Room?.Code; if (code == null) return;
            state.Sent[p.Id] = new Sent { At = Time.realtimeSinceStartup }; Render();
            string error = await backend.InviteAsync(p.Id, code);
            if (layer == null || invite != state) return;
            if (error != null)
            {
                state.Sent[p.Id].State = "no";
                Flash(error == ErrorCodes.InvitesDisabled ? L(p.Name + " davetleri kapattı", p.Name + " turned invites off") : L(p.Name + " davet edilemedi", "Could not invite " + p.Name), "bad");
            }
            else Flash(L(p.Name + " davet edildi", p.Name + " invited"), "good");
            Render();
        }

        // ---------- dialogs ----------
        public sealed class Dialog
        {
            public string Kind, Pin = "", Code = "", Error;
            public TableSummary Table;
            public InviteMessage Invite;
            public float Shake, OpenedAt;
            public bool First, Copied;
        }

        private Dialog dialog;
        public Dialog Current => dialog;

        private void OpenDialog(Dialog next) { next.OpenedAt = Time.realtimeSinceStartup; dialog = next; RenderDialog(); }
        private void CloseDialog() { dialog = null; RenderDialog(); }

        public void OpenCode() => OpenDialog(new Dialog { Kind = "code" });

        /// <summary>A friend's invite (realtime <c>invite</c>): who, the stake, Katıl joins by the table code.</summary>
        public void ShowInvite(InviteMessage message) => OpenDialog(new Dialog { Kind = "invited", Invite = message });

        private void RenderDialog()
        {
            if (dialogLayer == null) return;
            dialogLayer.Clear();
            var d = dialog; if (d == null) { dialogLayer.pickingMode = PickingMode.Ignore; return; }
            dialogLayer.pickingMode = PickingMode.Position;
            var shade = Fill("dialog-backdrop"); shade.pickingMode = PickingMode.Position; view.Decorate(shade, "rgba(0,0,0,.55)");
            Tap(shade, () => { if (d.Kind != "connect" && d.Kind != "offline") CloseDialog(); }); dialogLayer.Add(shade);
            var frame = Fill("dialog-frame"); frame.style.alignItems = Align.Center; frame.style.justifyContent = Justify.Center; dialogLayer.Add(frame);
            var card = Box("dialog-" + d.Kind); card.pickingMode = PickingMode.Position; card.style.alignItems = Align.Center;
            Raise(card, "var(--pn)", 26, 10, "var(--pnS)"); Border(card, 1, new Color(1, 1, 1, .1f)); GalaxyFilters.Shadow(card, 0, 40, 45, new Color(0, 0, 0, .6f));
            card.RegisterCallback<ClickEvent>(e => e.StopPropagation()); frame.Add(card); Pop(card, d.OpenedAt, .25f);
            switch (d.Kind)
            {
                case "connect":
                    card.style.width = 380; Pad(card, 30, 30, 30, 30);
                    var spin = Spinner(44, "var(--a1)"); Border(spin, 4, view.C("var(--a1)")); spin.style.borderTopColor = Color.clear; card.Add(spin);
                    var ct = Text(L("Masaya bağlanılıyor…", "Connecting to the table…"), 20, "Montserrat-700", "#F4F1E8"); ct.style.marginTop = 12; card.Add(ct);
                    if (d.Table != null) { var cb = Text(d.Table.Name + " · " + view.N(d.Table.Stake), 14, "Baloo2-700", "var(--mt)"); cb.style.marginTop = 12; card.Add(cb); }
                    break;
                case "pin": BuildPin(card, d); break;
                case "code": BuildCodeDialog(card, d); break;
                case "full":
                case "closed":
                    card.style.width = 450; Pad(card, 30, 30, 24, 30);
                    var badge = Box(); Square(badge, 68); Raise(badge, "linear-gradient(#FF7A86,#E8455A)", 22, 5, "#9E1F33"); badge.Add(view.Icon(d.Kind == "full" ? "table-full" : "table-closed", 32, "#fff")); card.Add(badge);
                    var et = Text(d.Kind == "full" ? L("Masa doldu", "Table is full") : L("Masa kapandı", "Table closed"), 24, "Montserrat-700", "#F4F1E8"); et.style.marginTop = 14; card.Add(et);
                    var eb = Text(d.Kind == "full" ? L("Son koltuk sen bağlanırken doldu.", "The last seat was taken while you were connecting.") : L("Oda sahibi masayı kapattı.", "The host closed the table."), 15, "Baloo2-700", "var(--mt)");
                    eb.style.whiteSpace = WhiteSpace.Normal; eb.style.maxWidth = 360; eb.style.unityTextAlign = TextAnchor.MiddleCenter; eb.style.marginTop = 10; card.Add(eb);
                    card.Add(Pair(L("Tamam", "OK"), CloseDialog, L("Benzer masa bul", "Find a similar table"), () => Similar(d.Table), "dialog-ok", "dialog-alt"));
                    break;
                case "share": BuildShare(card, d); break;
                case "offline":
                    card.style.width = 450; Pad(card, 30, 30, 24, 30);
                    var ob = Box(); Square(ob, 68); Raise(ob, "linear-gradient(#FF7A86,#E8455A)", 22, 5, "#9E1F33"); ob.Add(view.Icon("table-wifi-off", 32, "#fff")); card.Add(ob);
                    var ot = Text(L("Sunucuya bağlanılamadı", "Could not reach the server"), 24, "Montserrat-700", "#F4F1E8"); ot.style.marginTop = 14; card.Add(ot);
                    var obody = Text(L("Lobi için çevrimiçi olmalısın. Bağlantını kontrol edip yeniden dene.", "The lobby needs you online. Check your connection and try again."), 15, "Baloo2-700", "var(--mt)");
                    obody.style.whiteSpace = WhiteSpace.Normal; obody.style.maxWidth = 360; obody.style.unityTextAlign = TextAnchor.MiddleCenter; obody.style.marginTop = 10; card.Add(obody);
                    card.Add(Pair(L("Ana menü", "Main menu"), () => { CloseDialog(); Back(); }, L("Tekrar dene", "Try again"), async () => { CloseDialog(); if (await Connect()) Render(); }, "dialog-ok", "dialog-alt"));
                    break;
                case "invited":
                    var m = d.Invite; card.style.width = 450; Pad(card, 30, 30, 24, 30);
                    var face = Box(); Square(face, 68); view.Decorate(face, m.From?.Color ?? "#3D7BFF", 34); Border(face, 3, new Color(1, 1, 1, .85f)); face.Add(Text(Initials(m.From?.Name), 24, "Montserrat-700", "#fff")); card.Add(face);
                    var it = Text(L((m.From?.Name ?? "Bir arkadaşın") + " seni masaya çağırıyor", (m.From?.Name ?? "A friend") + " invites you to a table"), 22, "Montserrat-700", "#F4F1E8"); it.style.marginTop = 14; it.style.whiteSpace = WhiteSpace.Normal; it.style.unityTextAlign = TextAnchor.MiddleCenter; card.Add(it);
                    var ib = Text(L("Bahis " + view.N(m.Stake) + " · kod " + m.RoomCode, "Bet " + view.N(m.Stake) + " · code " + m.RoomCode), 15, "Baloo2-700", "var(--mt)"); ib.style.marginTop = 10; card.Add(ib);
                    card.Add(Pair(L("Sonra", "Later"), CloseDialog, L("Katıl", "Join"), () =>
                    {
                        if (m.Stake > backend.Chips) { CloseDialog(); Low(m.Stake, backend.Chips); return; }
                        var code = m.RoomCode; CloseDialog(); _ = JoinAsync(new TableJoinMessage { Code = code }, null);
                    }, "dialog-ok", "dialog-alt"));
                    break;
            }
        }

        private void BuildPin(VisualElement card, Dialog d)
        {
            card.style.width = 440; Pad(card, 30, 30, 24, 30);
            var icon = Box(); Square(icon, 68); Raise(icon, "linear-gradient(160deg,var(--a1),var(--a2))", 22, 5, "var(--aS)"); GalaxyFilters.Shadow(icon, 0, 0, 15, view.C("var(--gl)")); icon.Add(view.Icon("lock", 30, "var(--aT)")); card.Add(icon);
            var title = Text(L("Şifreli masa", "Password protected"), 24, "Montserrat-700", "#F4F1E8"); title.style.marginTop = 12; card.Add(title);
            var sub = Text(d.Table == null ? L("Kod " + d.Code, "Code " + d.Code) : L(d.Table.Name + " · " + view.N(d.Table.Stake) + " bahis", d.Table.Name + " · " + view.N(d.Table.Stake) + " bet"), 14, "Baloo2-700", "var(--mt)"); sub.style.marginTop = 12; card.Add(sub);
            var boxes = Row(); boxes.style.marginTop = 20; boxes.style.marginBottom = 2; boxes.name = "dialog-pin-boxes";
            var cells = new List<Label>();
            for (int i = 0; i < 4; i++)
            {
                var cell = Text("", 30, "Montserrat-700", "#F4F1E8"); cell.style.width = 58; cell.style.height = 66; cell.style.unityTextAlign = TextAnchor.MiddleCenter; if (i > 0) cell.style.marginLeft = 10;
                view.Decorate(cell, "var(--dp)", 16); cells.Add(cell); boxes.Add(cell);
            }
            var field = Input("dialog-pin", d.Pin, "", 16, "Baloo2-700", "#F4F1E8", 4);
            field.style.position = Position.Absolute; field.style.left = field.style.top = field.style.right = field.style.bottom = 0; field.style.opacity = 0;
            void Paint()
            {
                for (int i = 0; i < 4; i++)
                {
                    cells[i].text = i < d.Pin.Length ? "•" : "";
                    bool focus = i == Math.Min(d.Pin.Length, 3);
                    Border(cells[i], 2, d.Error != null ? view.C("#FF5E6C") : focus ? view.C("var(--a1)") : Color.clear);
                }
            }
            field.RegisterValueChangedCallback(e => { string digits = Regex.Replace(e.newValue ?? "", "\\D", ""); if (digits.Length > 4) digits = digits.Substring(0, 4); if (digits != e.newValue) field.SetValueWithoutNotify(digits); d.Pin = digits; d.Error = null; Paint(); ErrorLine(card, d); if (digits.Length == 4) SubmitPin(d); });
            field.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { e.StopPropagation(); SubmitPin(d); } }, TrickleDown.TrickleDown);
            boxes.Add(field); card.Add(boxes); Paint();
            Shake(boxes, d.Shake);
            ErrorLine(card, d);
            card.Add(Pair(L("Vazgeç", "Cancel"), CloseDialog, L("Gir", "Enter"), () => SubmitPin(d), "dialog-cancel", "dialog-submit", d.Pin.Length == 4));
            field.schedule.Execute(() => field.Focus()).ExecuteLater(30);
        }

        private void SubmitPin(Dialog d)
        {
            if (d.Pin.Length != 4) { d.Error = L("4 rakam gir", "Enter 4 digits"); d.Shake = Time.realtimeSinceStartup; RenderDialog(); return; }
            _ = JoinAsync(new TableJoinMessage { TableId = d.Table?.Id, Code = d.Table == null ? d.Code : null, Pin = d.Pin }, d.Table);
        }

        private void BuildCodeDialog(VisualElement card, Dialog d)
        {
            card.style.width = 480; card.style.alignItems = Align.Stretch; Pad(card, 26, 28, 24, 28);
            var head = Row();
            var icon = Box(); Square(icon, 46); Raise(icon, "var(--pnH)", 14, 4, "var(--pnS)"); icon.Add(view.Icon("key", 22, "var(--a1)")); head.Add(icon);
            var words = Box(); words.style.marginLeft = 12; words.style.flexShrink = 1;
            words.Add(Text(L("Kodla katıl", "Join by code"), 22, "Montserrat-700", "#F4F1E8"));
            var s = Text(L("Özel masalar lobide görünmez; arkadaşından aldığın kodu yaz.", "Private tables are hidden from the lobby; type the code a friend sent you."), 13, "Baloo2-700", "var(--mt)"); s.style.whiteSpace = WhiteSpace.Normal; words.Add(s);
            head.Add(words); card.Add(head);
            var box = Row(); box.name = "dialog-code-box"; box.style.height = 62; box.style.marginTop = 14; Pad(box, 0, 18, 0, 18); view.Decorate(box, "var(--dp)", 16);
            Border(box, 2, view.C(d.Error != null ? "#FF5E6C" : "var(--a1)"));
            var field = Input("dialog-code", d.Code, "GX-0000", 26, "Montserrat-700", "var(--a1)", 8); field.style.flexGrow = 1; MenuView.Tracking(field, 5, 26);
            field.RegisterValueChangedCallback(e => { string up = (e.newValue ?? "").ToUpperInvariant(); if (up != e.newValue) field.SetValueWithoutNotify(up); d.Code = up; if (d.Error != null) { d.Error = null; Border(box, 2, view.C("var(--a1)")); ErrorLine(card, d); } });
            field.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { e.StopPropagation(); SubmitCode(d); } }, TrickleDown.TrickleDown);
            box.Add(field); card.Add(box); Shake(box, d.Shake);
            ErrorLine(card, d);
            card.Add(Pair(L("Vazgeç", "Cancel"), CloseDialog, L("Katıl", "Join"), () => SubmitCode(d), "dialog-cancel", "dialog-submit"));
            field.schedule.Execute(() => field.Focus()).ExecuteLater(30);
        }

        /// <summary>GX-XXXX: letters and digits only; "GX7Q2M", "7q2m" and "gx-7q2m" all mean GX-7Q2M.</summary>
        public static string NormalizeCode(string input)
        {
            string text = (input ?? "").Trim().ToUpperInvariant();
            // "GX-…": what follows the dash is the code; otherwise a leading GX on six characters is the prefix.
            if (text.Contains("-")) text = text.Substring(text.IndexOf('-') + 1);
            string raw = Regex.Replace(text, "[^A-Z0-9]", "");
            if (raw.StartsWith("GX") && raw.Length == 6) raw = raw.Substring(2);
            return raw.Length == 4 ? "GX-" + raw : null;
        }

        private void SubmitCode(Dialog d)
        {
            var code = NormalizeCode(d.Code);
            if (code == null) { d.Error = L("Kod GX- ve 4 karakterden oluşur", "The code is GX- and 4 characters"); d.Shake = Time.realtimeSinceStartup; RenderDialog(); return; }
            _ = JoinAsync(new TableJoinMessage { Code = code }, null);
        }

        private void ErrorLine(VisualElement card, Dialog d)
        {
            var line = card.Q<Label>("dialog-error");
            if (line == null) { line = Text("", 14, "Baloo2-800", "#FF8A94"); line.name = "dialog-error"; line.style.minHeight = 20; line.style.marginTop = 10; line.style.alignSelf = Align.Center; card.Add(line); }
            line.text = d.Error ?? "";
        }

        private void BuildShare(VisualElement card, Dialog d)
        {
            var room = backend.Room; string code = room?.Code ?? "";
            card.style.width = 520; Pad(card, 30, 30, 24, 30);
            var ok = Box(); Square(ok, 64); Raise(ok, "#3CCB6E", 32, 5, "#22914A"); GalaxyFilters.Shadow(ok, 0, 0, 15, new Color(.235f, .8f, .43f, .5f)); ok.Add(view.Icon("tick", 30, "#fff")); card.Add(ok);
            var title = Text(L("Özel masan hazır", "Your private table is ready"), 24, "Montserrat-700", "#F4F1E8"); title.style.marginTop = 12; card.Add(title);
            var sub = Text(L("Masa lobide listelenmez. Kodu arkadaşlarına gönder, kodla katılsınlar.", "It isn’t listed in the lobby. Send the code to friends so they can join."), 14, "Baloo2-700", "var(--mt)");
            sub.style.whiteSpace = WhiteSpace.Normal; sub.style.maxWidth = 400; sub.style.unityTextAlign = TextAnchor.MiddleCenter; sub.style.marginTop = 12; card.Add(sub);
            var box = Box(); box.style.alignSelf = Align.Stretch; box.style.alignItems = Align.Center; box.style.marginTop = 16; Pad(box, 18, 0, 16, 0); view.Decorate(box, "rgba(0,0,0,.18)", 20); DashedBorder(box, 2, view.C("var(--bd)"));
            var cap = Text(L("DAVET KODU", "INVITE CODE"), 11, "Baloo2-800", "var(--mt)"); MenuView.Tracking(cap, 1.8f, 11); box.Add(cap);
            var big = Text(code, 44, "Montserrat-800", "var(--a1)"); big.name = "share-code"; MenuView.Tracking(big, 8, 44); big.style.marginTop = 6; box.Add(big);
            if (!string.IsNullOrEmpty(d.Pin))
            {
                var pin = Row(); pin.style.marginTop = 6; Pad(pin, 5, 12, 5, 12); view.Decorate(pin, "var(--dp)", 10);
                pin.Add(view.Icon("lock", 13, "var(--a1)")); var pl = Text(L("Şifre", "Password"), 13, "Baloo2-800", "var(--ic)"); pl.style.marginLeft = 8; pin.Add(pl);
                var pv = Text(d.Pin, 15, "Montserrat-700", "#F4F1E8"); MenuView.Tracking(pv, 4, 15); pv.style.marginLeft = 8; pin.Add(pv); box.Add(pin);
            }
            card.Add(box);
            string text = L("Galaxy 101'de \"" + room?.Table?.Name + "\" masasına gel! Kod: " + code + (string.IsNullOrEmpty(d.Pin) ? "" : " · Şifre: " + d.Pin), "Join my \"" + room?.Table?.Name + "\" table on Galaxy 101! Code: " + code + (string.IsNullOrEmpty(d.Pin) ? "" : " · Password: " + d.Pin));
            var buttons = Row(); buttons.style.alignSelf = Align.Stretch; buttons.style.marginTop = 12;
            var copy = Text(d.Copied ? L("Kopyalandı", "Copied") : L("Kodu kopyala", "Copy code"), 16, "Montserrat-700", d.Copied ? "#062B14" : "#F4F1E8"); copy.name = "share-copy"; copy.style.flexGrow = 1; copy.style.flexBasis = 0; Pad(copy, 13, 0, 13, 0); copy.style.unityTextAlign = TextAnchor.MiddleCenter;
            Raise(copy, d.Copied ? "#3CCB6E" : "var(--pnH)", 16, 5, d.Copied ? "#22914A" : "var(--pnS)"); Tap(copy, () => { view.Copy(code); d.Copied = true; RenderDialog(); }); buttons.Add(copy);
            var share = Text(L("Paylaş", "Share"), 16, "Montserrat-700", "#fff"); share.name = "share-share"; share.style.flexGrow = 1; share.style.flexBasis = 0; share.style.marginLeft = 10; Pad(share, 13, 0, 13, 0); share.style.unityTextAlign = TextAnchor.MiddleCenter;
            Raise(share, "#3D7BFF", 16, 5, "#2148B0"); Tap(share, () => { view.Copy(text); Flash(L("Davet metni kopyalandı", "Invite text copied"), "good"); }); buttons.Add(share);
            card.Add(buttons);
            var room2 = Text(d.First ? L("Bekleme odasına geç", "Go to waiting room") : L("Odaya dön", "Back to room"), 17, "Montserrat-800", "var(--aT)"); room2.name = "share-room"; room2.style.alignSelf = Align.Stretch; room2.style.marginTop = 14; Pad(room2, 15, 0, 15, 0); room2.style.unityTextAlign = TextAnchor.MiddleCenter;
            Raise(room2, "linear-gradient(var(--a1),var(--a2))", 16, 5, "var(--aS)"); Tap(room2, CloseDialog); card.Add(room2);
        }

        private VisualElement Pair(string first, Action firstGo, string second, Action secondGo, string firstName, string secondName, bool secondOn = true)
        {
            var row = Row(); row.style.alignSelf = Align.Stretch; row.style.marginTop = 14;
            var a = Text(first, 16, "Montserrat-700", "#F4F1E8"); a.name = firstName; a.style.flexGrow = 1; a.style.flexBasis = 0; Pad(a, 14, 0, 14, 0); a.style.unityTextAlign = TextAnchor.MiddleCenter; Raise(a, "var(--pnH)", 16, 5, "var(--pnS)"); Tap(a, firstGo); row.Add(a);
            var b = Text(second, 16, "Montserrat-800", secondOn ? "var(--aT)" : "var(--mt)"); b.name = secondName; b.style.flexGrow = 1; b.style.flexBasis = 0; b.style.marginLeft = 10; Pad(b, 14, 0, 14, 0); b.style.unityTextAlign = TextAnchor.MiddleCenter;
            Raise(b, secondOn ? "linear-gradient(var(--a1),var(--a2))" : "var(--pnH)", 16, 5, secondOn ? "var(--aS)" : "var(--pnS)"); Tap(b, secondGo); row.Add(b);
            return row;
        }

        /// <summary>lbShake .4 s: ±8 px.</summary>
        private void Shake(VisualElement element, float at)
        {
            if (at <= 0) return;
            Animate(() => { float t = (Time.realtimeSinceStartup - at) / .4f; element.style.translate = new Translate(t >= 1 || t < 0 ? 0 : 8 * Mathf.Sin(t * Mathf.PI * 4) * (1 - t * .3f), 0); });
        }
    }
}
