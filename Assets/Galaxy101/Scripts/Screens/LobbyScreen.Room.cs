using System;
using System.Collections.Generic;
using System.Linq;
using Galaxy101.Contracts;
using Galaxy101.Online;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    public sealed partial class LobbyScreen
    {
        // POS of waitVals(): bottom (you), right, top, left inside the 872 × 592 table area at (28, 100).
        private static readonly Vector2[] SeatSpots = { new Vector2(436, 520), new Vector2(800, 308), new Vector2(436, 92), new Vector2(72, 308) };
        private float roomShownAt, countdownAt;
        private int countdownFrom;
        private bool leaving, copied;
        private readonly Dictionary<Guid, string> seen = new Dictionary<Guid, string>();
        /// <summary>w.keys: who sat in each seat when it last popped in (lbPop .4s plays when a seat fills).</summary>
        private readonly Dictionary<int, (Guid Who, float At)> seatPops = new Dictionary<int, (Guid, float)>();

        /// <summary>The flashes of waitTick / addBot: "X masaya oturdu" (ok), "Bot koltuğa oturdu" (info).</summary>
        private void NoteSeats(TableStateMessage state)
        {
            var now = state.Table.Seats.Where(s => s?.Player != null).ToDictionary(s => s.Player.Id, s => s.Player.Name);
            if (seen.Count > 0)
                foreach (var joined in now.Where(p => !seen.ContainsKey(p.Key) && p.Key != backend.Me))
                {
                    bool bot = state.Table.Seats.Any(s => s?.Player?.Id == joined.Key && s.Bot);
                    if (bot) Flash(S("botAdded"), "info"); else Flash(S("joined", joined.Value), "ok");
                    // A friend who accepted sits down: the row's invite is done (Masada).
                    if (invite != null && invite.Sent.ContainsKey(joined.Key)) invite.Sent.Remove(joined.Key);
                }
            seen.Clear(); foreach (var p in now) seen[p.Key] = p.Value;
        }

        public int Countdown => countdownFrom <= 0 ? 0 : Mathf.Max(0, Mathf.CeilToInt(countdownFrom - (Time.realtimeSinceStartup - countdownAt)));

        private void BuildRoom()
        {
            var room = backend.Room; var t = room.Table; int mine = Mathf.Clamp(room.YourSeat, 0, 3);
            bool host = t.HostId == backend.Me, pairs = t.Type == "pairs";
            int seated = t.Seats.Count(s => s != null); bool full = seated == 4;
            int counting = Countdown;
            var page = Fill("lobby-room"); page.pickingMode = PickingMode.Ignore; body.Add(page);
            FadeIn(page, roomShownAt, .3f);

            // Top bar (86, padding 0 28, gap 14): Ayrıl, BEKLEME ODASI (+ Oda sahibi sensin) over the name, Davet et, the code box, chips.
            var bar = Row(); MenuView.Place(bar, 0, 0, 1280, 86); Pad(bar, 0, 28, 0, 28); page.Add(bar);
            var leave = Row(); leave.name = "lobby.w.leave"; leave.style.height = 46; Pad(leave, 0, 16, 0, 12); Raise(leave, "var(--pnH)", 14, 4, "var(--pnS)"); leave.tooltip = S("leaveRoom");
            var li = view.Icon(IconLeave, 20, "var(--ic)"); li.style.marginRight = 8; leave.Add(li); leave.Add(Text(S("leaveRoom"), 15, "Montserrat-700", "#F4F1E8"));
            GalaxyFilters.HoverBrightness(leave, 1.25f); Tap(leave, () => _ = Leave()); bar.Add(leave);
            var titles = Box(); titles.style.marginLeft = 14; titles.style.flexShrink = 1;
            var tag = Row(); var cap = Text(S("waitRoom"), 11, "Baloo2-800", "var(--a1)", 0, "lobby.text.waitRoom"); MenuView.Tracking(cap, 1.8f, 11); tag.Add(cap);
            if (host) { var own = Text(S("youOwn"), 11, "Baloo2-800", "var(--a1)", 0, "lobby.text.youOwn"); own.style.marginLeft = 10; Pad(own, 2, 8, 2, 8); view.Decorate(own, "rgba(255,201,74,.16)", 7); tag.Add(own); }
            titles.Add(tag);
            var name = Text(t.Name, 24, "Montserrat-700", "#F4F1E8", 1.1f, "lobby.w.name"); name.style.marginTop = 3; name.style.maxWidth = 520; name.style.overflow = Overflow.Hidden; name.style.textOverflow = TextOverflow.Ellipsis; titles.Add(name);
            bar.Add(titles); bar.Add(Grow());
            if (!full && counting == 0)
            {
                var inv = Row(); inv.name = "lobby.w.openInv"; inv.style.height = 46; inv.style.marginLeft = 14; Pad(inv, 0, 18, 0, 14); Raise(inv, "linear-gradient(var(--a1),var(--a2))", 14, 4, "var(--aS)");
                var ii = view.Icon(IconInvite, 16, "var(--aT)"); ii.style.marginRight = 8; inv.Add(ii); inv.Add(Text(S("invite"), 15, "Montserrat-800", "var(--aT)"));
                int pending = invite?.Sent.Values.Count(x => x.State == "pend") ?? 0;
                if (pending > 0) { var n = Text(pending.ToString(), 11, "Baloo2-800", "var(--a1)", 20f / 11, "lobby.w.pendN"); n.style.marginLeft = 8; n.style.minWidth = 20; Pad(n, 0, 6, 0, 6); n.style.unityTextAlign = TextAnchor.MiddleCenter; view.Decorate(n, "var(--aT)", 10); inv.Add(n); }
                GalaxyFilters.HoverBrightness(inv, 1.08f); Tap(inv, () => OpenInvite(null)); bar.Add(inv);
            }
            if (room.Code != null && t.Private)
            {
                // gap 10, 46 tall, padding 0 6 0 16, radius 14, 1px dashed --bd, rgba(0,0,0,.2).
                var codeBox = Row(); codeBox.style.height = 46; codeBox.style.marginLeft = 14; Pad(codeBox, 0, 6, 0, 16); view.Decorate(codeBox, "rgba(0,0,0,.2)", 14); DashedBorder(codeBox, 1, view.C("var(--bd)"));
                codeBox.Add(Text(S("code"), 12, "Baloo2-800", "var(--mt)", 0, "lobby.text.code"));
                var code = Text(room.Code, 18, "Montserrat-700", "var(--a1)", 0, "lobby.w.code"); code.style.marginLeft = 10; MenuView.Tracking(code, 3, 18); codeBox.Add(code);
                var copy = Text(copied ? S("copied") : S("copy"), 13, "Montserrat-700", "#F4F1E8", 0, "lobby.w.copy"); copy.style.marginLeft = 10; copy.style.height = 34; copy.style.unityTextAlign = TextAnchor.MiddleCenter; Pad(copy, 0, 12, 0, 12); Raise(copy, "var(--pnH)", 10, 3, "var(--pnS)");
                Tap(copy, CopyCode); codeBox.Add(copy);
                var share = Box("lobby.w.share"); Square(share, 34); share.style.marginLeft = 10; Raise(share, "var(--pnH)", 10, 3, "var(--pnS)"); share.Add(view.Icon(IconShare, 16, "var(--ic)")); share.tooltip = S("share");
                Tap(share, () => OpenDialog(new Dialog { Kind = "share", SharePin = roomPin })); codeBox.Add(share);
                bar.Add(codeBox);
            }
            bar.Add(Balance(backend.Chips, false));

            // The oval table with GALAXY 101, the pool, the status and four seat dots; the seats around it.
            var area = Box(); MenuView.Place(area, 28, 100, 872, 592); page.Add(area);
            // box-shadow: inset 0 0 0 3px rgba(255,255,255,.08), inset 0 0 0 10px --dp, inset 0 0 0 12px --a2, 0 0 60px --gl,
            // 0 24px 50px rgba(0,0,0,.45). The glow comes from a filled twin behind the table (a ring would glow thin).
            var halo = Box(); MenuView.Place(halo, 156, 176, 560, 264); view.Decorate(halo, "var(--dp)", 132); GalaxyFilters.Shadow(halo, 0, 0, 30, view.C("var(--gl)")); area.Add(halo);
            var oval = Box("lobby-room-table"); MenuView.Place(oval, 156, 176, 560, 264);
            view.Decorate(oval, "radial-gradient(ellipse at 50% 40%,var(--pnH),var(--sd) 75%)", 132);
            Border(oval, 12, view.C("var(--dp)")); GalaxyFilters.Shadow(oval, 0, 24, 25, new Color(0, 0, 0, .45f)); area.Add(oval);
            // Absolute children start inside the 12 px border: −2 puts the gold band 10–12 px in, −12 the sheen at the edge.
            var rim = Box(); rim.style.position = Position.Absolute; rim.style.left = rim.style.top = rim.style.right = rim.style.bottom = -2; Radius(rim, 122); Border(rim, 2, view.C("var(--a2)")); oval.Add(rim);
            var sheen = Box(); sheen.style.position = Position.Absolute; sheen.style.left = sheen.style.top = sheen.style.right = sheen.style.bottom = -12; Radius(sheen, 132); Border(sheen, 3, new Color(1, 1, 1, .08f)); oval.Add(sheen);
            var middle = Box(); MenuView.Place(middle, 156, 176, 560, 264); middle.style.alignItems = Align.Center; middle.style.justifyContent = Justify.Center; area.Add(middle);
            // 800 22px/1 --fL, letter-spacing 4px, gradient --lg1 → --a1 → --a2 clipped to the text (UF-801: --a1).
            var brand = Text("GALAXY 101", 22, "Montserrat-800", "var(--a1)", 1); MenuView.Tracking(brand, 4, 22); middle.Add(brand);
            var pool = Row(); pool.style.marginTop = 8; Pad(pool, 6, 14, 6, 14); Pill(pool, "rgba(0,0,0,.28)", 6 + 21 + 6);
            pool.Add(Coin(18, false));
            var pl = Text(S("pool"), 13, "Baloo2-800", "var(--mt)", 0, "lobby.text.pool"); pl.style.marginLeft = 8; pool.Add(pl);
            var pv = Text(view.N(t.Stake * 4), 17, "Montserrat-700", "#F4F1E8", 0, "lobby.w.pool"); pv.style.marginLeft = 8; pool.Add(pv); middle.Add(pool);
            var status = Text(StatusLine(full, seated, counting), 14, "Baloo2-800", full ? "#8EF0B0" : "var(--ic)", 0, "lobby.w.status"); status.style.marginTop = 8; middle.Add(status);
            var dots = Row(); dots.style.marginTop = 8;
            for (int s = 0; s < 4; s++) { var d = Box(); Square(d, 10); if (s > 0) d.style.marginLeft = 6; view.Decorate(d, t.Seats[(s + mine) % 4] != null ? "var(--a1)" : "rgba(255,255,255,.15)", 5); dots.Add(d); }
            middle.Add(dots);
            if (counting > 0) Animate(() => { int left = Countdown; status.text = StatusLine(full, seated, left); }, status);

            // The host's crown sits on the host's seat; a room whose owner has no seat (the prototype's enterWait(t, true)
            // on a table you are not at) crowns mk()'s owner: 0.
            int crowned = t.Seats.FindIndex(s => s?.Player != null && s.Player.Id == t.HostId); if (crowned < 0) crowned = 0;
            for (int spot = 0; spot < 4; spot++) area.Add(Seat(t, (spot + mine) % 4, spot, host, pairs, counting > 0, crowned));

            // The summary panel (right 28, top 100, bottom 28, 330 wide, padding 20, gap 14, radius 24, 1px rgba(255,255,255,.08), 0 8px 0 --pnS).
            var panel = Box("lobby-room-summary"); panel.style.position = Position.Absolute; panel.style.right = 28; panel.style.top = 100; panel.style.height = 720 - 100 - 28; panel.style.width = 330;
            Pad(panel, 20, 20, 20, 20); Border(panel, 1, new Color(1, 1, 1, .08f)); Raise(panel, "var(--pn)", 24, 8, "var(--pnS)"); page.Add(panel);
            var head = Row(); head.name = "wait.summary.head"; head.style.justifyContent = Justify.SpaceBetween; head.style.height = 24;
            var sc = Text(S("summary"), 11, "Baloo2-800", "var(--mt)", 0, "lobby.text.summary"); MenuView.Tracking(sc, 1.8f, 11); head.Add(sc);
            var ids = Ids(t, 0); ids.style.flexWrap = Wrap.NoWrap; head.Add(ids); panel.Add(head);
            var stats = Stats(t, "var(--sd)", "wait.summary.stats"); stats.style.marginTop = 14; panel.Add(stats);
            var rules = RulesList(t, "var(--sd)", "wait.summary.rules", "lobby.text.kRules"); rules.style.marginTop = 14; panel.Add(rules);
            // padding 11 14, gap 10, radius 14, rgba(255,201,74,.1), inset 0 0 0 1px rgba(255,201,74,.22).
            var prize = Row(); prize.name = "wait.summary.prize"; prize.style.marginTop = 14; Pad(prize, 10, 13, 10, 13); view.Decorate(prize, "rgba(255,201,74,.1)", 14); Border(prize, 1, new Color(1, .79f, .29f, .22f));
            prize.Add(view.Icon(IconTrophy, 22, "var(--a1)"));
            var pt = Text(pairs ? S("prizeSumE", view.N(t.Stake * 4), view.N(t.Stake * 2)) : S("prizeSum", view.N(t.Stake * 4)), 13, "Baloo2-700", "var(--ic)", 1.35f, "lobby.w.sum.prize");
            Paragraph(pt, 1.35f); pt.style.flexShrink = 1; pt.style.marginLeft = 10; prize.Add(pt); panel.Add(prize);
            var hint = Text(host ? full ? S("hintReady") : S("hintOwner") : S("hintGuest"), 13, "Baloo2-700", "var(--mt)", 1.4f, "lobby.w.hint");
            Paragraph(hint, 1.4f); hint.style.unityTextAlign = TextAnchor.MiddleCenter; hint.style.marginTop = 14; panel.Add(hint);
            bool ready = host && full && counting == 0;
            string label = counting > 0 ? S("starting", counting) : host ? full ? S("start") : S("startN", seated) : S("guestWait");
            var start = Row(); start.name = "lobby.w.start"; start.style.height = 62; start.style.marginTop = 14; start.style.justifyContent = Justify.Center;
            Raise(start, ready ? "linear-gradient(#5BE38A,#2BB45F)" : "var(--pnH)", 18, 5, ready ? "#18793F" : "var(--pnS)");
            if (!host || counting > 0) { var spin = Spinner(18, ready ? "#05301A" : "var(--mt)"); spin.style.marginRight = 10; start.Add(spin); }
            var sl = Text(label, 20, "Montserrat-800", ready ? "#05301A" : "var(--mt)"); start.Add(sl);
            if (counting > 0) Animate(() => { int left = Countdown; sl.text = left > 0 ? S("starting", left) : S("connT"); }, sl);
            if (ready) { Pulse(start, 2f); Tap(start, () => _ = Start()); }
            panel.Add(start);

            if (InviteOpen) BuildInvite(t, host, counting > 0);
        }

        private string StatusLine(bool full, int seated, int counting) => counting > 0 ? S("starting", counting) : full ? S("ready") : S("waitN", seated);

        /// <summary>One seat of w.seats: a filled one (face, crown, kick, tag, name, team) or an empty one (dots, Boş koltuk, Davet, Bot ekle).</summary>
        private VisualElement Seat(TableSummary t, int seat, int spot, bool host, bool pairs, bool counting, int crowned)
        {
            var at = SeatSpots[spot];
            // transform: translate(-50%,-50%), width 200, column, centred, gap 6.
            var slot = Box("lobby-seat-" + spot); MenuView.Place(slot, at.x - 100, at.y - 100, 200, 200); slot.style.alignItems = Align.Center; slot.style.justifyContent = Justify.Center;
            var entry = t.Seats[seat];
            if (entry?.Player != null)
            {
                bool me = entry.Player.Id == backend.Me;
                var holder = Box(); Square(holder, 76);
                // box-shadow: 0 0 0 3px --pnS, 0 10px 24px rgba(0,0,0,.4) — a --pnS disc 3 px larger behind the face.
                var ring = Box(); MenuView.Place(ring, -3, -3, 82, 82); view.Decorate(ring, "var(--pnS)", 41); GalaxyFilters.Shadow(ring, 0, 10, 12, new Color(0, 0, 0, .4f)); holder.Add(ring);
                var face = Box("lobby.z.ini[" + spot + "]"); MenuView.Place(face, 0, 0, 76, 76); face.style.alignItems = Align.Center; face.style.justifyContent = Justify.Center;
                view.Decorate(face, entry.Bot ? "#5B6390" : entry.Player.Color ?? "#3D7BFF", 38); Border(face, 3, view.C(me ? "var(--a1)" : "rgba(255,255,255,.85)"));
                face.Add(Text(Initials(entry.Player.Name), 24, "Montserrat-700", "#fff")); holder.Add(face);
                if (seat == crowned) { var crown = view.Icon(IconCrown, 26, null); crown.style.height = 20; crown.style.position = Position.Absolute; crown.style.top = -14; crown.style.left = 25; crown.tooltip = S("ownerT"); holder.Add(crown); }
                if (host && !me && !counting)
                {
                    var kick = Box("lobby.z.kick[" + spot + "]"); Square(kick, 26); kick.style.position = Position.Absolute; kick.style.right = -6; kick.style.top = -4; Raise(kick, "#FF5E6C", 13, 3, "#C23E4A");
                    kick.Add(view.Icon(IconClose, 11, "#fff")); kick.tooltip = S("kick"); string who = entry.Player.Name;
                    GalaxyFilters.HoverBrightness(kick, 1.1f); Tap(kick, () => _ = Kick(seat, who)); holder.Add(kick);
                }
                string tagText = entry.Bot ? S("bot") : me ? S("you") : (En ? "LV " : "SV ") + Math.Max(1, entry.Player.Level);
                var tag = Text(tagText, 10, "Baloo2-800", entry.Bot ? "#fff" : me ? "var(--aT)" : "var(--ic)", 0, "lobby.z.tag[" + spot + "]"); Pad(tag, 1, 8, 1, 8); view.Decorate(tag, entry.Bot ? "#5B6390" : me ? "var(--a1)" : "var(--pnH)", 7);
                tag.style.position = Position.Absolute; tag.style.bottom = -8; tag.style.alignSelf = Align.Center; holder.Add(tag);
                slot.Add(holder);
                SeatPop(holder, spot, entry.Player.Id);
                var label = Text(entry.Player.Name, 16, "Montserrat-700", "#F4F1E8", 0, "lobby.z.name[" + spot + "]"); label.style.marginTop = 6 + 6; slot.Add(label);
                if (pairs)
                {
                    // Seats 0 + 2 are Takım A, 1 + 3 Takım B (i % 2).
                    bool a = spot % 2 == 0; var team = Text(a ? S("teamA") : S("teamB"), 11, "Baloo2-800", "#fff", 0, "lobby.z.team[" + spot + "]"); team.style.marginTop = 6; Pad(team, 2, 9, 2, 9); view.Decorate(team, a ? "#3D7BFF" : "#FF6B6B", 7); slot.Add(team);
                }
                return slot;
            }
            seatPops.Remove(spot);
            var empty = Box(); Square(empty, 76); view.Decorate(empty, "rgba(0,0,0,.18)", 38); DashedBorder(empty, 2, view.C("var(--bd)"));
            var dots = Row();
            for (int i = 0; i < 3; i++)
            {
                // lbDots 1.2s, delays 0 / .2 / .4 s: opacity .25 → 1 → .25 (ease).
                var d = Box(); Square(d, 6); if (i > 0) d.style.marginLeft = 4; view.Decorate(d, "var(--mt)", 3); dots.Add(d); float phase = i * .2f;
                Animate(() => { float p = Mathf.Repeat((Time.realtimeSinceStartup - phase) / 1.2f, 1); float k = MenuView.CssBezier(.25f, .1f, .25f, 1, p < .5f ? p * 2 : (p - .5f) * 2); d.style.opacity = p < .5f ? Mathf.Lerp(.25f, 1, k) : Mathf.Lerp(1, .25f, k); }, d);
            }
            empty.Add(dots); slot.Add(empty);
            var el = Text(S("emptySeat"), 14, "Baloo2-700", "var(--mt)", 0, "lobby.text.emptySeat[" + spot + "]"); el.style.marginTop = 6; slot.Add(el);
            if (!counting)
            {
                var buttons = Row(); buttons.style.marginTop = 6;
                var inv = Row(); inv.name = "lobby.z.invite[" + spot + "]"; Pad(inv, 7, 12, 7, 12); Raise(inv, "linear-gradient(var(--a1),var(--a2))", 11, 3, "var(--aS)");
                var ii = view.Icon(IconSeatInvite, 14, "var(--aT)"); ii.style.marginRight = 6; inv.Add(ii); inv.Add(Text(S("inviteS"), 13, "Montserrat-800", "var(--aT)"));
                GalaxyFilters.HoverBrightness(inv, 1.08f); Tap(inv, () => OpenInvite(seat)); buttons.Add(inv);
                if (host)
                {
                    var bot = Row(); bot.name = "lobby.z.addBot[" + spot + "]"; bot.style.marginLeft = 6; Pad(bot, 7, 13, 7, 13); Raise(bot, "var(--pnH)", 11, 3, "var(--pnS)");
                    var bi = view.Icon(IconBot, 15, "var(--ic)"); bi.style.marginRight = 6; bot.Add(bi); bot.Add(Text(S("addBot"), 13, "Montserrat-700", "#F4F1E8"));
                    GalaxyFilters.HoverBrightness(bot, 1.2f); Tap(bot, () => _ = AddBot(seat)); buttons.Add(bot);
                }
                slot.Add(buttons);
            }
            return slot;
        }

        /// <summary>lbPop .4s both: scale .5 → 1.08 (70 %, opaque) → 1, when a seat fills (not on every redraw).</summary>
        private void SeatPop(VisualElement holder, int spot, Guid who)
        {
            if (!seatPops.TryGetValue(spot, out var pop) || pop.Who != who) seatPops[spot] = pop = (who, Time.realtimeSinceStartup);
            float began = pop.At; if (Time.realtimeSinceStartup - began >= .4f) return;
            Animate(() =>
            {
                float u = Mathf.Clamp01((Time.realtimeSinceStartup - began) / .4f), e = MenuView.CssBezier(.25f, .1f, .25f, 1, u);
                float s = e < .7f ? Mathf.Lerp(.5f, 1.08f, e / .7f) : Mathf.Lerp(1.08f, 1, (e - .7f) / .3f);
                holder.style.scale = new Scale(Vector2.one * s); holder.style.opacity = Mathf.Clamp01(e / .7f);
            }, holder);
        }

        private void CopyCode()
        {
            var code = backend.Room?.Code; if (code == null) return;
            view.Copy(code); copied = true; Render();
            layer?.schedule.Execute(() => { copied = false; if (screen == "wait") Render(); }).ExecuteLater(1600);
        }

        /// <summary>leaveWait(): tableLeave refunds this player's stake; K-12 transfers ownership when another human remains.</summary>
        private async System.Threading.Tasks.Task Leave()
        {
            leaving = true; invite = null; inviteOpen = false;
            string error = await backend.LeaveAsync();
            if (layer == null) return;
            if (error != null && error != ErrorCodes.NotFound && error != "E_NOT_SEATED") { leaving = false; Flash(L("Ayrılamadın (" + error + ")", "Could not leave (" + error + ")"), "bad"); return; }
            // RoomChanged can already have rebuilt the list before LeaveAsync completes. Keep that live tree.
            if ((backend.Room == null || error != null) && screen != "list") { screen = "list"; animateCards = true; Render(); }
            Flash(S("leftRoom"), "info");
        }

        private async System.Threading.Tasks.Task AddBot(int seat)
        {
            string error = await backend.AddBotAsync(seat);
            if (error != null && layer != null && ErrorText(error) is string text) Flash(text, "bad");
        }

        private async System.Threading.Tasks.Task Kick(int seat, string who)
        {
            string error = await backend.KickAsync(seat);
            if (layer == null) return;
            if (error != null) { if (ErrorText(error) is string text) Flash(text, "bad"); }
            else Flash(S("kicked", who), "bad");
        }

        private async System.Threading.Tasks.Task Start()
        {
            string error = await backend.StartAsync();
            if (error != null && layer != null && ErrorText(error) is string text) Flash(text, "bad");
        }

        // ---------- invite (S14 Arkadaşlar, S15 Son oynadıkların) ----------
        public sealed class InviteState
        {
            public string Tab = "friends", Query = "";
            public int? Seat;
            public float OpenedAt;
            public bool QueryFocused;
            public List<FriendResponse> Friends;
            public List<RecentPlayer> Recent;
            public readonly Dictionary<Guid, Sent> Sent = new Dictionary<Guid, Sent>();
        }

        /// <summary>w.sent[name]: "pend" (30 s), "no" (declined), "exp" (no answer).</summary>
        public sealed class Sent { public string State = "pend"; public float At; }

        private InviteState invite;
        public InviteState Invite => invite;
        /// <summary>inviteSend: an invite lasts 30 s (protocol inviteSend / invSub).</summary>
        public const float InviteSeconds = 30;

        /// <summary>openInv / z.invite: inv {tab: 'fr', q: '', seat} — the sent invites carry over.</summary>
        public void OpenInvite(int? seat)
        {
            invite = invite ?? new InviteState(); inviteOpen = true;
            invite.Tab = "friends"; invite.Seat = seat; invite.Query = ""; invite.OpenedAt = Time.realtimeSinceStartup;
            Render();
            _ = LoadInvitees();
        }

        private async System.Threading.Tasks.Task LoadInvitees()
        {
            var state = invite; if (state == null) return;
            var friends = await backend.FriendsAsync(); var recent = await backend.RecentAsync();
            if (invite != state) return;
            state.Friends = friends ?? new List<FriendResponse>();
            state.Recent = (recent ?? new List<RecentPlayer>()).Where(r => r.Friend?.Player != null && state.Friends.All(f => f.Player?.Id != r.Friend.Player.Id)).ToList();
            if (screen == "wait") Render();
        }

        private bool inviteOpen;
        public bool InviteOpen => inviteOpen && invite != null;
        /// <summary>iv.close: the panel goes, the sent invites stay (their countdowns and the count on Davet et).</summary>
        private void CloseInvite() { inviteOpen = false; Render(); }

        private void BuildInvite(TableSummary t, bool host, bool counting)
        {
            var state = invite; int free = t.Seats.Count(s => s == null);
            // iv.open: z 25; the backdrop lobby.iv.close (rgba(0,0,0,.55) + blur(6px), lbFade .2s) is the persistent veil (Veil()).
            // 900 × 580 inside a 1 px border, grid minmax(0,1fr) 290px, radius 28; lbModal .32s cubic-bezier(.22,1,.36,1).
            var modal = Row(); modal.name = "lobby-invite"; modal.pickingMode = PickingMode.Position; modal.style.alignItems = Align.Stretch; MenuView.Place(modal, 189, 69, 902, 582); modal.style.overflow = Overflow.Hidden;
            Border(modal, 1, new Color(1, 1, 1, .1f)); Raise(modal, "var(--pn)", 28, 10, "var(--pnS)"); GalaxyFilters.BoxShadow(modal, 0, 40, 45, new Color(0, 0, 0, .6f));
            modal.RegisterCallback<ClickEvent>(e => e.StopPropagation()); Pop(modal, state.OpenedAt, .32f, false, true); modalLayer.Add(modal);

            // Left: padding 22 22 0 28, gap 14.
            var left = Box(); left.style.flexGrow = 1; left.style.flexBasis = 0; Pad(left, 22, 22, 0, 28); modal.Add(left);
            var head = Row();
            // The header's person icon: its "+" bar is drawn at x 24–30, outside the 24-unit view box, so only the stem shows.
            var icon = Box(); Square(icon, 50); Raise(icon, "linear-gradient(160deg,var(--a1),var(--a2))", 16, 4, "var(--aS)"); icon.Add(ClippedIcon(IconInviteHead, 24, "var(--aT)")); head.Add(icon);
            var words = Box(); words.style.marginLeft = 14; words.style.flexShrink = 1;
            words.Add(Text(S("invT"), 26, "Montserrat-700", "#F4F1E8", 1.1f, "lobby.text.invT"));
            // invSub(n): "n boş koltuk · davet 30 sn geçerli" (EN seat / seats) or "Masa dolu · davet gönderilemez" (EK-412).
            int open = counting ? 0 : free;
            var sub = Text(open > 0 ? L(open + " boş koltuk · davet 30 sn geçerli", open + " empty " + (open > 1 ? "seats" : "seat") + " · invites last 30 s") : L("Masa dolu · davet gönderilemez", "Table is full · no invites"), 13, "Baloo2-700", "var(--mt)", 0, "lobby.iv.sub");
            sub.style.marginTop = 3; words.Add(sub);
            head.Add(words); left.Add(head);

            var tools = Row(); tools.style.marginTop = 14;
            // Tabs: gap 4, padding 4, radius 14, --dp, inset 0 2px 0 rgba(0,0,0,.3); a tab 36 tall, padding 0 14, gap 7, 800 14px; its count 900 11px at .75.
            var tabs = Segment(4); tabs.name = "lobby-invite-tabs"; view.InsetRing(tabs, 14)("rest");
            int online = state.Friends?.Count(f => f.Presence == "online") ?? 0;
            var tabList = new[] { ("friends", S("tabFr"), state.Friends == null ? "…" : online + "/" + state.Friends.Count), ("recent", S("tabRe"), state.Recent == null ? "…" : state.Recent.Count.ToString()) };
            for (int i = 0; i < tabList.Length; i++)
            {
                var (value, label, n) = tabList[i]; string pick = value; bool on = state.Tab == value;
                var tab = SegmentOption("lobby.b.pick[" + i + "]", label, on, () => { state.Tab = pick; Render(); }, 14, 0, 14, i == 0 ? 4 : 0, 10, "Montserrat-800"); tab.style.height = 36;
                var count = Text(n, 11, "Baloo2-800", on ? "var(--aT)" : "var(--mt)", 0, "lobby.b.n[" + i + "]"); count.style.marginLeft = 7; count.style.opacity = .75f; tab.Add(count); tabs.Add(tab);
            }
            tools.Add(tabs);
            // Search: flex 1, 44 tall, padding 0 14, gap 8, radius 14, --dp, qRing.
            var search = Row(); search.name = "lobby-invite-search"; search.style.flexGrow = 1; search.style.height = 44; search.style.marginLeft = 10; Pad(search, 0, 14, 0, 14); view.Decorate(search, "var(--dp)", 14);
            var ring = FieldRing(search, 14); ring(state.QueryFocused ? "focus" : "rest");
            search.Add(view.Icon(IconSearch, 16, "var(--mt)"));
            var field = Input("lobby.iv.onQ", state.Query, S("invSearch"), 14, "Baloo2-700", "#F4F1E8", 24); field.style.flexGrow = 1; field.style.marginLeft = 8;
            field.RegisterCallback<FocusInEvent>(_ => { state.QueryFocused = true; ring("focus"); });
            field.RegisterCallback<FocusOutEvent>(_ => { state.QueryFocused = false; ring("rest"); });
            search.Add(field); tools.Add(search); left.Add(tools);

            // Rows: flex 1, gap 8, padding 2 6 20 0; the thin scrollbar takes no room in the reference (rows 554 wide, UF-802).
            var list = view.Scroll(false); list.name = "lobby-invite-list"; list.verticalScrollerVisibility = ScrollerVisibility.Hidden; list.style.flexGrow = 1; list.style.marginTop = 14;
            list.contentContainer.style.paddingTop = 2; list.contentContainer.style.paddingRight = 6; list.contentContainer.style.paddingBottom = 20; left.Add(list);
            void FillRows()
            {
                list.Clear();
                bool recent = state.Tab == "recent";
                var pool = recent ? state.Recent?.Select(r => (r.Friend, r.PlayedAt)).ToList() : state.Friends?.Select(f => (f, (DateTimeOffset?)null)).ToList();
                if (pool == null) { var wait = Spinner(26, "var(--mt)"); wait.style.alignSelf = Align.Center; wait.style.marginTop = 40; list.Add(wait); return; }
                var tr = new System.Globalization.CultureInfo("tr-TR"); string q = state.Query.Trim().ToLower(tr);
                // rows: the query on the name, then online, in a game, offline (a stable sort: the server's order within each).
                int Order(FriendResponse f) => f.Presence == "online" ? 0 : f.Presence == "playing" ? 1 : 2;
                var rows = pool.Where(f => f.Item1?.Player != null && (q.Length == 0 || f.Item1.Player.Name.ToLower(tr).Contains(q))).OrderBy(f => Order(f.Item1)).ToList();
                if (rows.Count == 0) { var none = Text(S("invNone"), 14, "Baloo2-700", "var(--mt)", 0, "lobby.text.invNone"); none.style.alignSelf = Align.Center; Pad(none, 40, 0, 40, 0); list.Add(none); return; }
                for (int i = 0; i < rows.Count; i++) { var row = InviteRow(rows[i].Item1, rows[i].Item2, recent, i, t, counting ? 0 : free); if (i > 0) row.style.marginTop = 8; list.Add(row); }
            }
            FillRows();
            field.RegisterValueChangedCallback(e => { state.Query = e.newValue ?? ""; FillRows(); });

            // Right: padding 22, gap 14, --sd, border-left 1px rgba(255,255,255,.06).
            var right = Box("lobby-invite-side"); right.style.width = 290; Pad(right, 22, 22, 22, 22); view.Decorate(right, "var(--sd)", 0); right.style.borderLeftWidth = 1; right.style.borderLeftColor = new Color(1, 1, 1, .06f); modal.Add(right);
            var top = Row(); top.style.justifyContent = Justify.FlexEnd;
            var close = Box("lobby.iv.close#2"); Square(close, 40); Raise(close, "var(--pnH)", 12, 3, "var(--pnS)"); close.Add(view.Icon(IconClose, 14, "var(--ic)")); close.tooltip = S("close"); GalaxyFilters.HoverBrightness(close, 1.2f); Tap(close, CloseInvite); top.Add(close); right.Add(top);
            var seatsCap = Text(S("invSeats"), 11, "Baloo2-800", "var(--mt)", 0, "lobby.text.invSeats"); MenuView.Tracking(seatsCap, 1.8f, 11); seatsCap.style.marginTop = 14; right.Add(seatsCap);
            // grid repeat(4,1fr), gap 8, aspect-ratio 1: (245 − 3 × 8) / 4 = 55.25.
            var seats = Row(); seats.style.marginTop = 14;
            int mine = Mathf.Clamp(backend.Room?.YourSeat ?? 0, 0, 3);
            for (int spot = 0; spot < 4; spot++)
            {
                int s = (spot + mine) % 4;
                var chip = SeatChip(t.Seats[s], 55.25f, 14, "lobby.z.ini[" + spot + "]", state.Seat == s); if (spot > 0) chip.style.marginLeft = 8;
                seats.Add(chip);
            }
            right.Add(seats);
            int pending = state.Sent.Values.Count(x => x.State == "pend");
            // invSeatLine(n, p): "k/4 dolu" + " · p davet bekliyor" while invites wait (EK-412).
            var line = Text(L((4 - free) + "/4 dolu" + (pending > 0 ? " · " + pending + " davet bekliyor" : ""), (4 - free) + "/4 taken" + (pending > 0 ? " · " + pending + " pending" : "")), 13, "Baloo2-700", "var(--ic)", 1.4f, "lobby.iv.seatLine");
            line.style.marginTop = 14; right.Add(line);
            var rule = Box(); rule.style.height = 1; rule.style.marginTop = 14 + 4; rule.style.marginBottom = 4; rule.style.backgroundColor = new Color(1, 1, 1, .07f); right.Add(rule);
            var room = backend.Room;
            if (t.Private && room?.Code != null)
            {
                var cc = Text(S("invByCode"), 11, "Baloo2-800", "var(--mt)", 0, "lobby.text.invByCode"); MenuView.Tracking(cc, 1.8f, 11); cc.style.marginTop = 14; right.Add(cc);
                // gap 4, padding 14 0, radius 18, 2px dashed --bd, rgba(0,0,0,.18); the code 800 30px/1.1 --fL, letter-spacing 5px (UF-801).
                var codeBox = Box(); codeBox.style.alignItems = Align.Center; codeBox.style.marginTop = 14; Pad(codeBox, 14, 0, 14, 0); view.Decorate(codeBox, "rgba(0,0,0,.18)", 18); DashedBorder(codeBox, 2, view.C("var(--bd)"));
                var code = Text(room.Code, 30, "Montserrat-800", "var(--a1)", 1.1f, "lobby.w.code#2"); MenuView.Tracking(code, 5, 30); codeBox.Add(code); right.Add(codeBox);
                var buttons = Row(); buttons.style.marginTop = 14;
                var copy = Text(copied ? S("copied") : S("copy"), 14, "Montserrat-700", "#F4F1E8", 0, "lobby.w.copy#2"); copy.style.flexGrow = 1; copy.style.flexBasis = 0; copy.style.height = 42; copy.style.unityTextAlign = TextAnchor.MiddleCenter; Raise(copy, "var(--pnH)", 13, 4, "var(--pnS)"); Tap(copy, CopyCode); buttons.Add(copy);
                var share = Text(S("share"), 14, "Montserrat-700", "#fff", 0, "lobby.w.share#2"); share.style.flexGrow = 1; share.style.flexBasis = 0; share.style.marginLeft = 8; share.style.height = 42; share.style.unityTextAlign = TextAnchor.MiddleCenter; Raise(share, "#3D7BFF", 13, 4, "#2148B0"); Tap(share, () => OpenDialog(new Dialog { Kind = "share", SharePin = roomPin })); buttons.Add(share);
                right.Add(buttons);
            }
            else
            {
                // iv.openTbl: gap 10, padding 12 14, radius 14, rgba(255,255,255,.04); the info icon 18 (margin-top 1).
                var note = Row(); note.style.alignItems = Align.FlexStart; note.style.marginTop = 14; Pad(note, 12, 14, 12, 14); view.Decorate(note, "rgba(255,255,255,.04)", 14);
                var info = view.Icon(IconInfo, 18, "var(--mt)"); info.style.marginTop = 1; note.Add(info);
                var nt = Text(S("invOpen"), 13, "Baloo2-700", "var(--mt)", 1.4f, "lobby.text.invOpen");
                Paragraph(nt, 1.4f); nt.style.flexShrink = 1; nt.style.flexGrow = 1; nt.style.marginLeft = 10; note.Add(nt); right.Add(note);
            }
            right.Add(Grow());
            var foot = Text(S("invNote"), 12, "Baloo2-700", "var(--mt)", 1.4f, "lobby.text.invNote"); Paragraph(foot, 1.4f); foot.style.unityTextAlign = TextAnchor.MiddleCenter; right.Add(foot);
        }

        /// <summary>One iv.rows entry: avatar with the presence dot, name + level, the presence line, and the state button.</summary>
        private VisualElement InviteRow(FriendResponse f, DateTimeOffset? playedAt, bool recent, int index, TableSummary t, int free)
        {
            var p = f.Player; bool seated = t.Seats.Any(s => s?.Player?.Id == p.Id);
            invite.Sent.TryGetValue(p.Id, out var sent);
            string st = seated ? "in" : sent?.State ?? (f.Presence == "online" ? "can" : f.Presence == "playing" ? "game" : "off");
            float left = sent != null && st == "pend" ? InviteSeconds - (Time.realtimeSinceStartup - sent.At) : 0;
            if (st == "pend" && left <= 0) { sent.State = st = "exp"; }
            // r.ring: pend → inset 0 0 0 2px rgba(255,201,74,.45), in → rgba(60,203,110,.4) (the padding gives it back).
            float ring = st == "pend" || st == "in" ? 2 : 0;
            var row = Row(); row.name = "lobby-invite-row-" + p.Id; Pad(row, 10 - ring, 12 - ring, 10 - ring, 10 - ring); view.Decorate(row, "var(--sd)", 16);
            if (st == "pend") Border(row, 2, new Color(1, .79f, .29f, .45f)); else if (st == "in") Border(row, 2, new Color(.235f, .8f, .43f, .4f));
            row.style.opacity = f.Presence == "offline" && st != "in" ? .55f : 1;
            var holder = Box(); Square(holder, 46);
            var face = Box("lobby.r.ini[" + index + "]"); Square(face, 46); view.Decorate(face, p.Color ?? "#3D7BFF", 23); Border(face, 2, new Color(1, 1, 1, .8f)); face.Add(Text(Initials(p.Name), 16, "Montserrat-700", "#fff")); holder.Add(face);
            var dot = Box(); Square(dot, 14); dot.style.position = Position.Absolute; dot.style.right = -1; dot.style.bottom = -1; view.Decorate(dot, f.Presence == "online" ? "#3CCB6E" : f.Presence == "playing" ? "#FFC94A" : "#6B7199", 7); Border(dot, 3, view.C("var(--sd)")); holder.Add(dot);
            row.Add(holder);
            var words = Box(); words.style.flexGrow = 1; words.style.flexShrink = 1; words.style.marginLeft = 14; words.style.overflow = Overflow.Hidden;
            var nameLine = Row(); var name = Text(p.Name, 17, "Montserrat-700", "#F4F1E8", 1.2f, "lobby.r.name[" + index + "]"); name.style.flexShrink = 1; name.style.overflow = Overflow.Hidden; name.style.textOverflow = TextOverflow.Ellipsis; nameLine.Add(name);
            var lv = Text((En ? "LV " : "SV ") + Math.Max(1, p.Level), 10, "Baloo2-800", "var(--ic)", 0, "lobby.r.lv[" + index + "]"); lv.style.marginLeft = 8; Pad(lv, 1, 7, 1, 7); view.Decorate(lv, "var(--pnH)", 6); nameLine.Add(lv); words.Add(nameLine);
            // sub: Çevrimiçi / Oyunda / Son görülme ‹seen›; a recent player's line adds when you played together.
            string subText = f.Presence == "online" ? S("stOn") : f.Presence == "playing" ? S("stGame") : S("stOff", f.LastSeen is DateTimeOffset seenAt ? SeenAgo(seenAt) : "").TrimEnd();
            if (recent && playedAt is DateTimeOffset played) subText += " · " + PlayedAgo(played);
            var subLine = Text(subText, 12, "Baloo2-700", f.Presence == "online" ? "#8EF0B0" : f.Presence == "playing" ? "var(--a1)" : "var(--mt)", 0, "lobby.r.sub[" + index + "]");
            subLine.style.marginTop = 2; subLine.style.overflow = Overflow.Hidden; subLine.style.textOverflow = TextOverflow.Ellipsis; words.Add(subLine); row.Add(words);

            string label, bg, fg, shade; bool can = false, check = false;
            if (st == "in") { label = S("bIn"); bg = "rgba(60,203,110,.16)"; fg = "#8EF0B0"; shade = "transparent"; check = true; }
            else if (st == "pend") { label = S("bPend", Mathf.Max(0, Mathf.CeilToInt(left))); bg = "var(--pnH)"; fg = "#F4F1E8"; shade = "var(--pnS)"; can = true; }
            else if (st == "no") { label = S("bNo"); bg = "rgba(255,94,108,.14)"; fg = "#FF9AA3"; shade = "transparent"; }
            else if (st == "game") { label = S("bGame"); bg = "var(--dp)"; fg = "var(--mt)"; shade = "rgba(0,0,0,.25)"; }
            else if (st == "off") { label = S("bOff"); bg = "var(--dp)"; fg = "var(--mt)"; shade = "rgba(0,0,0,.25)"; }
            else if (free == 0) { label = S("bFull"); bg = "var(--dp)"; fg = "var(--mt)"; shade = "rgba(0,0,0,.25)"; }
            else if (st == "exp") { label = S("bExp"); bg = "var(--pnH)"; fg = "#F4F1E8"; shade = "var(--pnS)"; can = true; }
            else { label = S("bInv"); bg = "linear-gradient(var(--a1),var(--a2))"; fg = "var(--aT)"; shade = "var(--aS)"; can = true; }
            // r.go: min-width 136, 40 tall, padding 0 14, gap 8, radius 12, 0 4px 0 shade, 800 14px, overflow hidden.
            var button = Row(); button.name = "lobby.r.go[" + index + "]"; button.style.minWidth = 136; button.style.height = 40; button.style.marginLeft = 14; button.style.justifyContent = Justify.Center; Pad(button, 0, 14, 0, 14); button.style.overflow = Overflow.Hidden;
            Raise(button, bg, 12, 4, shade);
            if (st == "pend")
            {
                // The 3 px --a1 bar shrinks with the time left (width 1s linear); a 14 px spinner (2.5px currentColor).
                var bar = Box(); bar.style.position = Position.Absolute; bar.style.left = 0; bar.style.bottom = 0; bar.style.height = 3; bar.style.backgroundColor = view.C("var(--a1)"); button.Add(bar);
                var spin = Spinner(14, fg, 2.5f); spin.style.marginRight = 8; button.Add(spin);
                button.tooltip = S("bCancel");
                var text = Text(label, 14, "Montserrat-800", fg); button.Add(text); var s = sent; string who = p.Name;
                Animate(() =>
                {
                    float remaining = InviteSeconds - (Time.realtimeSinceStartup - s.At);
                    // left = 30 − whole seconds gone; the bar eases to each new value over 1 s (transition: width 1s linear).
                    bar.style.width = Length.Percent(Mathf.Clamp01((remaining + 1) / InviteSeconds) * 100);
                    if (remaining <= 0 && s.State == "pend") { s.State = "exp"; Flash(S("invExp", who), "info"); Render(); return; }
                    text.text = S("bPend", Mathf.Max(0, Mathf.CeilToInt(remaining)));
                }, button);
            }
            else
            {
                if (check) { var tick = view.Icon(IconCheck, 15, fg); tick.style.marginRight = 8; button.Add(tick); }
                button.Add(Text(label, 14, "Montserrat-800", fg));
            }
            if (can) Tap(button, () => _ = SendInvite(p, recent, st == "pend"));
            row.Add(button);
            return row;
        }

        /// <summary>sendInvite(fr) / the pending button's cancel: inviteSend (into the picked seat) or inviteCancel.</summary>
        private async System.Threading.Tasks.Task SendInvite(PlayerSummary p, bool recent, bool cancel)
        {
            var state = invite; if (state == null) return;
            if (cancel) { state.Sent.Remove(p.Id); Render(); _ = backend.CancelInviteAsync(p.Id); return; }
            state.Sent[p.Id] = new Sent { At = Time.realtimeSinceStartup };
            Flash(S("invSent", p.Name), "info"); Render();
            string error = await backend.InviteAsync(p.Id, state.Seat, recent);
            if (layer == null || invite != state || error == null) return;
            // A refused send (offline, busy, not friends …) is silent in the package (S14): the row offers the invite again.
            state.Sent.Remove(p.Id);
            if (ErrorText(error) is string text) Flash(text, "bad");
            else if (error == ErrorCodes.InvitesDisabled) Flash(L(p.Name + " davetleri kapattı", p.Name + " turned invites off"), "bad");
            Render();
        }

        /// <summary>inviteResult: declined → Reddetti (invNo, bad), expired → Tekrar davet (invExp); accepted shows as the player sitting down.</summary>
        private void OnInviteAnswered(Guid player, string result)
        {
            var state = invite; if (layer == null || state == null || !state.Sent.TryGetValue(player, out var sent) || sent.State != "pend") return;
            string who = state.Friends?.FirstOrDefault(f => f.Player?.Id == player)?.Player?.Name ?? state.Recent?.FirstOrDefault(r => r.Friend?.Player?.Id == player)?.Friend.Player.Name ?? "";
            switch (result)
            {
                case "declined": sent.State = "no"; Flash(S("invNo", who), "bad"); break;
                case "expired": sent.State = "exp"; Flash(S("invExp", who), "info"); break;
                case "failed": state.Sent.Remove(player); break;
                default: return;
            }
            if (screen == "wait") Render();
        }
    }
}
