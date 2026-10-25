using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    /// <summary>The Duo / Squad invite, "Maç bulundu" with its 10 s accept, and the result card (G101-Dereceli.dc.html lines 253–353).</summary>
    public sealed partial class RankedScreen
    {
        /// <summary>A full-screen layer: the dimmed, blurred backdrop (rkFade .2s) and a centring frame for the card.</summary>
        private VisualElement Overlay(string shade, float began, Action onShade, string shadeName = null)
        {
            modalLayer.pickingMode = PickingMode.Ignore;
            var root = Fill(); root.pickingMode = PickingMode.Position; modalLayer.Add(root);
            var dim = Fill(shadeName); dim.pickingMode = PickingMode.Position; view.Decorate(dim, shade); GalaxyFilters.BackdropBlur(dim, 6); root.Add(dim);
            if (onShade != null) Tap(dim, onShade);
            FadeIn(root, began, .2f);
            var frame = Fill(); frame.style.alignItems = Align.Center; frame.style.justifyContent = Justify.Center; root.Add(frame);
            return frame;
        }

        /// <summary>The modal card: --pn, 1 px rgba(255,255,255,.1) border, 0 10px 0 --pnS and 0 40px 90px rgba(0,0,0,.6); rkModal (translateY 16 px, scale .96) with cubic-bezier(.2,1.2,.4,1).</summary>
        private VisualElement ModalCard(int radius, float began, float seconds)
        {
            var card = Column(); card.pickingMode = PickingMode.Position;
            Border(card, 1, new Color(1, 1, 1, .1f)); Raise(card, "var(--pn)", radius, 10, "var(--pnS)"); GalaxyFilters.Shadow(card, 0, 40, 45, new Color(0, 0, 0, .6f));
            card.RegisterCallback<ClickEvent>(e => e.StopPropagation());
            Enter(card, began, seconds, u => Bezier(.2f, 1.2f, .4f, 1, u), k =>
            {
                card.style.opacity = Mathf.Clamp01(k); card.style.translate = new Translate(0, 16 * (1 - k)); card.style.scale = new Scale(Vector2.one * Mathf.LerpUnclamped(.96f, 1, k));
            });
            return card;
        }

        /// <summary>CSS width is the content box (no box-sizing on the cards): add the padding and the border.</summary>
        private static void ContentWidth(VisualElement card, float width) =>
            card.style.width = width + card.style.paddingLeft.value.value + card.style.paddingRight.value.value + card.style.borderLeftWidth.value + card.style.borderRightWidth.value;

        // ---------- Dereceliye davet et ----------
        private void BuildInvite()
        {
            var frame = Overlay("rgba(0,0,0,.55)", inviteOpenedAt, CloseInvite, "ranked.closeInvite");
            var card = ModalCard(26, inviteOpenedAt, .28f); ContentWidth(card, 520); frame.Add(card);
            var head = Row(); Pad(head, 20, 24, 6, 24);
            var icon = Box(); Square(icon, 44); Raise(icon, "linear-gradient(160deg,#7AA6FF,#3D6BF0)", 14, 4, "#2448B8"); icon.Add(view.Icon("v4/ranked-close-invite", 22, "#fff")); head.Add(icon);
            var words = Column(); words.style.flexGrow = 1; words.style.flexShrink = 1; words.style.marginLeft = 12;
            words.Add(Text(L("Dereceliye davet et", "Invite to ranked"), 22, "Montserrat-700", "#F4F1E8", 1.15f, "ranked.inv.title"));
            words.Add(Text(Mode == "duo" ? L("Duo · 1 arkadaş seç", "Duo · pick 1 friend") : L("Squad · " + party.Count + "/3 davet edildi", "Squad · " + party.Count + "/3 invited"), 13, "Baloo2-700", "var(--mt)", 0, "ranked.inv.sub"));
            head.Add(words);
            var close = Box("ranked.closeInvite#2"); Square(close, 38); close.style.marginLeft = 12; Raise(close, "var(--pnH)", 12, 3, "var(--pnS)");
            close.Add(view.Icon("v4/menu-close-settings", 14, "var(--ic)")); GalaxyFilters.HoverBrightness(close, 1.2f); Tap(close, CloseInvite); head.Add(close);
            card.Add(head);

            var list = Column(); Pad(list, 12, 24, 24, 24);
            int need = Need(); bool full = party.Count >= need;
            var friends = backend.Friends ?? new List<RankedPlayer>();
            for (int i = 0; i < friends.Count; i++)
            {
                var f = friends[i]; var inParty = party.FirstOrDefault(m => m.Friend.Id == f.Id); bool ok = f.Online && InRange(f.Idx);
                string btn = inParty != null ? (inParty.In ? L("Takımda", "In party") : L("Gönderildi", "Sent")) : !f.Online ? L("Çevrimdışı", "Offline") : !ok ? L("Aralık dışı", "Out of range") : L("Davet et", "Invite");
                bool can = inParty == null && ok && !full; var at = RankedLadder.At(f.Idx);
                var row = Row(); Pad(row, 10, 12, 10, 12); view.Decorate(row, "var(--sd)", 16); row.style.opacity = f.Online ? 1 : .5f; if (i > 0) row.style.marginTop = 8;
                var ini = Disc(42, f.Color, Initials(f.Name), 14, "ranked.f.ini[" + i + "]");
                // 12 px status dot + 2 px --sd border (content-box: 16 px), right/bottom −1 px.
                var dot = Box(); dot.style.position = Position.Absolute; dot.style.right = dot.style.bottom = -1; dot.style.width = dot.style.height = 16;
                view.Decorate(dot, f.Online ? "#3CCB6E" : "#5B6390", 8); Border(dot, 2, view.C("var(--sd)")); ini.Add(dot);
                row.Add(ini);
                var fw = Column(); fw.style.flexGrow = 1; fw.style.flexShrink = 1; fw.style.minWidth = 0; fw.style.marginLeft = 12;
                fw.Add(Text(f.Name, 16, "Montserrat-700", "#F4F1E8", 0, "ranked.f.name[" + i + "]"));
                var line = Row(); line.style.marginTop = 2; line.Add(Emblem(at.League.Key, at.Tier, 20));
                var lt = Text(RankedLadder.Label(f.Idx, Lang), 12, "Baloo2-800", at.League.Text); lt.style.marginLeft = 5; line.Add(lt);
                var note = Text(!ok && f.Online ? L("· yalnızca ±1 lig", "· ±1 league only") : "", 12, "Baloo2-700", "var(--mt)", 0, "ranked.f.note[" + i + "]"); note.style.marginLeft = 5; line.Add(note);
                fw.Add(line); row.Add(fw);
                var go = Text(btn, 14, "Montserrat-800", can ? "#fff" : "var(--mt)", 0, "ranked.f.go[" + i + "]"); go.style.marginLeft = 12; Pad(go, 9, 15, 9, 15);
                Raise(go, can ? "#3CCB6E" : "var(--pnH)", 12, 4, can ? "#22914A" : "var(--pnS)");
                if (!ok && f.Online) go.tooltip = L("Dereceli takımlar ±1 lig içinde olmalı", "Ranked parties must be within ±1 league");
                GalaxyFilters.HoverBrightness(go, 1.12f); var pick = f; Tap(go, () => { if (can) Invite(pick); });
                row.Add(go); list.Add(row);
            }
            card.Add(list);
        }

        // ---------- Maç bulundu / Hazır mısınız? ----------
        private readonly Dictionary<string, float> acceptSeen = new Dictionary<string, float>();

        private void BuildFound()
        {
            var f = Found; bool squad = Mode == "squad";
            var frame = Overlay("rgba(0,0,0,.6)", foundAt, null);
            var card = ModalCard(28, foundAt, .3f); card.style.alignItems = Align.Center; Pad(card, 28, 28, 24, 28); ContentWidth(card, 640); frame.Add(card);
            var kicker = Text(squad ? L("SQUAD HAZIR", "SQUAD READY") : L("MAÇ BULUNDU", "MATCH FOUND"), 12, "Baloo2-800", "var(--a1)", 0, "ranked.found.kicker"); MenuView.Tracking(kicker, 2, 12); card.Add(kicker);
            var title = Text(squad ? L("Hazır mısınız?", "Ready check") : L("Rakiplerin hazır", "Your opponents are here"), 30, "Montserrat-700", "#F4F1E8", 1.1f, "ranked.found.title"); title.style.marginTop = 16; card.Add(title);

            var grid = Row(); grid.style.alignSelf = Align.Stretch; grid.style.alignItems = Align.Stretch; grid.style.marginTop = 16;
            bool allOk = ready || f.Players.Select((p, i) => p.Me ? acceptedByMe : i < f.Accepted.Count && f.Accepted[i]).All(x => x);
            for (int i = 0; i < f.Players.Count; i++)
            {
                var p = f.Players[i]; var at = RankedLadder.At(p.Idx);
                bool ok = p.Me ? acceptedByMe : i < f.Accepted.Count && f.Accepted[i];
                int team = Mode == "duo" ? (i % 2 == 0 ? 0 : 1) : -1; // D-29: you and your partner at seats 0 and 2
                var cell = Column(); cell.style.flexGrow = 1; cell.style.flexBasis = 0; cell.style.minWidth = 0; cell.style.alignItems = Align.Center; Pad(cell, 14, 6, 12, 6); view.Decorate(cell, "var(--sd)", 18);
                if (p.Me) InsetRing(cell, 2, "var(--a1)", 18);
                if (i < f.Players.Count - 1) cell.style.marginRight = 12;
                var ini = Disc(56, p.Color, Initials(p.Name), 19, "ranked.p.ini[" + i + "]");
                if (ok)
                {
                    var badge = Box(); badge.style.position = Position.Absolute; badge.style.right = badge.style.bottom = -4; Square(badge, 30); view.Decorate(badge, "#3CCB6E", 15); Border(badge, 3, view.C("var(--sd)"));
                    badge.Add(view.Icon("v4/form-field-eye-7", 12, "#fff")); ini.Add(badge);
                    string key = "ok:" + i; if (!acceptSeen.TryGetValue(key, out float seen)) acceptSeen[key] = seen = Time.realtimeSinceStartup;
                    Enter(badge, seen, .3f, Linear, u => Pop(badge, u, .7f, .6f, 1.08f, 0, 0, .25f, .1f, .25f, 1)); // rkSeat .3s (ease)
                }
                cell.Add(ini);
                var name = Clip(Text(p.Name, 14, "Montserrat-700", "#F4F1E8", 0, "ranked.p.name[" + i + "]")); name.style.maxWidth = 130; name.style.marginTop = 6; cell.Add(name);
                var line = Row(); line.style.marginTop = 6; line.Add(Emblem(at.League.Key, at.Tier, 20));
                var lt = Text(RankedLadder.Label(p.Idx, Lang), 11, "Baloo2-800", at.League.Text); lt.style.marginLeft = 4; line.Add(lt); cell.Add(line);
                string teamText = team == 0 ? L("TAKIM A", "TEAM A") : team == 1 ? L("TAKIM B", "TEAM B") : p.Me ? L("SEN", "YOU") : "";
                var tl = Text(teamText, 10, "Baloo2-800", team == 1 ? "#FF8A94" : team == 0 ? "#8EF0B0" : "var(--a1)", 0, "ranked.p.team[" + i + "]"); MenuView.Tracking(tl, 1, 10); tl.style.marginTop = 6; cell.Add(tl);
                grid.Add(cell);
            }
            card.Add(grid);

            acceptLabel = null; acceptBar = null;
            if (!acceptedByMe)
            {
                var buttons = Row(); buttons.style.alignSelf = Align.Stretch; buttons.style.alignItems = Align.Stretch; buttons.style.marginTop = 20;
                var decline = Text(L("Reddet", "Decline"), 17, "Montserrat-700", "#F4F1E8", 0, "ranked.decline"); decline.style.flexGrow = 1; decline.style.flexBasis = 0; decline.style.unityTextAlign = TextAnchor.MiddleCenter;
                Pad(decline, 14, 0, 14, 0); Raise(decline, "var(--pnH)", 15, 5, "var(--pnS)"); GalaxyFilters.HoverBrightness(decline, 1.2f); Tap(decline, Decline); buttons.Add(decline);
                var accept = Box("ranked.accept"); accept.style.flexGrow = 2; accept.style.flexBasis = 0; accept.style.marginLeft = 12; accept.style.alignItems = Align.Center; Pad(accept, 14, 0, 14, 0);
                Raise(accept, "linear-gradient(#5BE38A,#2BB45F)", 15, 5, "#1A7A40");
                acceptBar = Box(); acceptBar.style.position = Position.Absolute; acceptBar.style.left = acceptBar.style.top = acceptBar.style.bottom = 0; acceptBar.style.backgroundColor = new Color(1, 1, 1, .18f);
                acceptBar.style.borderTopLeftRadius = acceptBar.style.borderBottomLeftRadius = 15; accept.Add(acceptBar);
                acceptLabel = Text(AcceptText(), 18, "Montserrat-800", "#fff", 0, "ranked.found.acceptLabel"); accept.Add(acceptLabel);
                GalaxyFilters.HoverBrightness(accept, 1.12f); Tap(accept, Accept); buttons.Add(accept);
                card.Add(buttons);
                Every();
            }
            else
            {
                var status = Row(); status.style.marginTop = 16; Pad(status, 12, 18, 12, 18); view.Decorate(status, "var(--sd)", 14);
                status.Add(Spinner(18, 3, "var(--a1)", .8f));
                var st = Text(allOk ? L("Masa hazırlanıyor…", "Setting up the table…") : L("Oyuncular bekleniyor…", "Waiting for players…"), 15, "Montserrat-800", "#F4F1E8"); st.style.marginLeft = 10; status.Add(st);
                card.Add(status);
            }
        }

        // ---------- result: summary card, then the promotion screen ----------
        private void BuildResult()
        {
            var r = result;
            var frame = Overlay("rgba(3,6,18,.72)", resultShownAt, null);
            if (resultStage == "promo") { BuildPromo(frame, r); return; }
            var to = RankedLadder.At(r.ToIdx); var league = to.League;
            var card = ModalCard(28, resultShownAt, .3f); card.style.alignItems = Align.Center; Pad(card, 30, 30, 24, 30); ContentWidth(card, 520); frame.Add(card);
            var kicker = Text(L("DERECELİ · ", "RANKED · ") + (r.Mode ?? "solo").ToUpperInvariant(), 12, "Baloo2-800", "var(--mt)", 0, "ranked.res.kicker"); MenuView.Tracking(kicker, 2, 12); card.Add(kicker);

            // Place colours (renderVals PC): gold, silver, bronze, then the panel's own.
            var pc = new[] { ("linear-gradient(#FFE58A,#E8A317)", "#A86E08", "#3A2400"), ("linear-gradient(#EEF2F8,#A9B4C6)", "#6C7890", "#1E2633"), ("linear-gradient(#F4C08C,#B8703A)", "#7A4520", "#2A1406"), ("var(--pnH)", "var(--pnS)", "#F4F1E8") }[Mathf.Clamp(r.Place, 1, 4) - 1];
            var top = Row(); top.style.marginTop = 12;
            var place = Text(r.Place + ".", 40, "Montserrat-800", pc.Item3, 0, "ranked.res.place"); Square(place, 84); place.style.unityTextAlign = TextAnchor.MiddleCenter; Raise(place, pc.Item1, 26, 6, pc.Item2); top.Add(place);
            var words = Column(); words.style.marginLeft = 18;
            string title = r.Place == 1 ? L("Galibiyet!", "Victory!") : L(r.Place + ". oldun", r.Place + new[] { "st", "nd", "rd", "th" }[Mathf.Clamp(r.Place, 1, 4) - 1] + " place");
            words.Add(Text(title, 26, "Montserrat-700", "#F4F1E8", 1.1f, "ranked.res.title"));
            var d = Tabular(Text((r.Delta > 0 ? "+" : "−") + Math.Abs(r.Delta) + " LP", 34, "Montserrat-800", r.Delta > 0 ? "#5BE38A" : "#FF8A94", 1, "ranked.res.d")); d.style.marginTop = 2; words.Add(d);
            top.Add(words); card.Add(top);

            var lp = Row(); lp.style.alignSelf = Align.Stretch; lp.style.marginTop = 16; Pad(lp, 14, 16, 14, 16); view.Decorate(lp, "var(--sd)", 18);
            lp.Add(Emblem(league.Key, to.Tier, 52, r.ToIdx >= RankedLadder.Steps ? backend.Profile.SeasonRank : 0));
            var lw = Column(); lw.style.flexGrow = 1; lw.style.flexShrink = 1; lw.style.minWidth = 0; lw.style.marginLeft = 14;
            lw.Add(Text(RankedLadder.Label(r.ToIdx, Lang), 17, "Montserrat-800", league.Text, 0, "ranked.res.label"));
            var track = Box(); track.style.height = 10; track.style.marginTop = 6; view.Decorate(track, "var(--dp)", 5);
            var fill = Box(); fill.style.height = Length.Percent(100); view.Decorate(fill, league.Bar, 5); track.Add(fill); lw.Add(track);
            var lpText = Text(r.ToIdx >= RankedLadder.Steps ? r.ToLp + " LP" : r.ToLp + " / 100 LP", 12, "Baloo2-800", "var(--mt)", 0, "ranked.res.lpText"); lpText.style.marginTop = 6; lw.Add(lpText);
            lp.Add(lw); card.Add(lp);
            BarMotion(fill, r);

            string note = r.Left ? L("Maçtan ayrıldın · 4. sayıldın", "You left the match · counted as 4th")
                : r.Event == "downTier" ? L(RankedLadder.Label(r.ToIdx, Lang) + " kademesine düştün", "Dropped to " + RankedLadder.Label(r.ToIdx, Lang))
                : r.Event == "downLeague" ? L(league.Tr + " ligine düştün · " + RankedLadder.Label(r.ToIdx, Lang), "Dropped to " + RankedLadder.Label(r.ToIdx, Lang))
                : r.Event == "protected" ? L("Düşme koruması seni bu ligde tuttu", "Demotion shield kept you in this league") : "";
            if (note.Length > 0)
            {
                bool shield = r.Event == "protected" && !r.Left;
                var n = Text(note, 14, "Baloo2-800", shield ? "#8EF0B0" : "#FF9AA3", 0, "ranked.res.note"); n.style.alignSelf = Align.Stretch; n.style.unityTextAlign = TextAnchor.MiddleCenter; n.style.marginTop = 12;
                Pad(n, 10, 14, 10, 14); view.Decorate(n, shield ? "rgba(60,203,110,.14)" : "rgba(255,94,108,.14)", 14); card.Add(n);
            }

            var buttons = Row(); buttons.style.alignSelf = Align.Stretch; buttons.style.marginTop = 18;
            var close = Text(r.Up ? L("Devam", "Continue") : L("Tamam", "Done"), 16, "Montserrat-700", "#F4F1E8", 0, "ranked.res.close"); close.style.flexGrow = 1; close.style.flexBasis = 0; close.style.unityTextAlign = TextAnchor.MiddleCenter;
            Pad(close, 13, 0, 13, 0); Raise(close, "var(--pnH)", 15, 5, "var(--pnS)"); GalaxyFilters.HoverBrightness(close, 1.2f); Tap(close, () => CloseResult(false)); buttons.Add(close);
            var again = Text(L("Tekrar ara", "Play again"), 16, "Montserrat-800", "var(--aT)", 0, "ranked.res.again"); again.style.flexGrow = 1; again.style.flexBasis = 0; again.style.unityTextAlign = TextAnchor.MiddleCenter; again.style.marginLeft = 12;
            Pad(again, 13, 0, 13, 0); Raise(again, "linear-gradient(var(--a1),var(--a2))", 15, 5, "var(--aS)"); GalaxyFilters.HoverBrightness(again, 1.07f); Tap(again, () => CloseResult(true)); buttons.Add(again);
            card.Add(buttons);
        }

        /// <summary>
        /// checkResult(): the bar starts at the old LP, eases (.9s cubic-bezier(.2,.9,.3,1)) after 350 ms to the new LP — or to
        /// full / empty when a tier was crossed, and from there at 1.4 s to the new tier's LP.
        /// </summary>
        private void BarMotion(VisualElement fill, RankedResult r)
        {
            bool up = r.Up, down = r.Event == "downTier" || r.Event == "downLeague";
            float w0 = r.FromLp, w1 = up ? 100 : down ? 0 : r.ToLp, w2 = r.ToIdx >= RankedLadder.Steps ? 100 : r.ToLp;
            float began = resultShownAt;
            Animate(() =>
            {
                float t = Time.realtimeSinceStartup - began, w;
                if (t < .35f) w = w0;
                else if (!(up || down) || t < 1.4f) w = Mathf.LerpUnclamped(w0, (up || down) ? w1 : w2, Bezier(.2f, .9f, .3f, 1, Mathf.Clamp01((t - .35f) / .9f)));
                else w = Mathf.LerpUnclamped(w1, w2, Bezier(.2f, .9f, .3f, 1, Mathf.Clamp01((t - 1.4f) / .9f)));
                fill.style.width = Length.Percent(Mathf.Clamp(w, 0, 100));
            }, fill);
        }

        private void BuildPromo(VisualElement frame, RankedResult r)
        {
            var to = RankedLadder.At(r.ToIdx); var league = to.League; float began = promoShownAt;
            var col = Column(); col.style.alignItems = Align.Center; frame.Add(col);
            // Rays: 720 px repeating-conic-gradient(glow 0 8deg, transparent 8deg 24deg), masked radial #000 10 % → transparent 62 %, rkRays 24 s.
            var rays = new Rays(view.C(league.Glow)); rays.style.position = Position.Absolute; rays.style.left = Length.Percent(50); rays.style.top = Length.Percent(40);
            rays.style.width = rays.style.height = 720; rays.style.marginLeft = rays.style.marginTop = -360; rays.style.opacity = r.Event == "tier" ? .45f : 1;
            col.Add(rays); Loop(rays, t => rays.style.rotate = new Rotate(Mathf.Repeat(t / 24, 1) * 360));
            int rank = backend.Profile.SeasonRank;
            string kick = r.Event == "tier" ? L("KADEME ATLADIN", "TIER UP") : r.Event == "galaxy" ? L("ZİRVEYE ULAŞTIN", "YOU REACHED THE TOP") : L("LİG ATLADIN", "LEAGUE UP");
            var pk = Text(kick, 13, "Baloo2-800", league.Text, 0, "ranked.res.pKicker"); MenuView.Tracking(pk, 2.4f, 13); col.Add(pk); Up(pk, began, .1f);
            var pop = Box(); pop.style.marginTop = 8; var emblem = Box(); emblem.style.width = emblem.style.height = 180; emblem.Add(Emblem(league.Key, to.Tier, 180, r.ToIdx >= RankedLadder.Steps ? rank : 0)); pop.Add(emblem); col.Add(pop);
            Enter(pop, began, .7f, Linear, u => Pop(pop, u, .6f, .4f, 1.08f, -12, 2, .2f, 1.3f, .4f, 1)); // rkPop .7s
            Loop(emblem, t => emblem.style.translate = new Translate(0, Time.realtimeSinceStartup - began < .8f ? 0 : FloatY(t, 3, .8f)));
            string title = r.Event == "league" ? L(league.Tr + " ligi", league.En + " league") : r.Event == "galaxy" ? (rank > 0 ? L("Galaksi · #" + rank, "Galaxy · #" + rank) : league.Name(Lang)) : RankedLadder.Label(r.ToIdx, Lang);
            var pt = Text(title, 38, "Montserrat-800", league.Text, 1.15f, "ranked.res.pTitle"); pt.style.marginTop = 8; col.Add(pt); Up(pt, began, .25f);
            string sub = r.Event == "league" ? L(RankedLadder.Label(r.ToIdx, Lang) + " kademesinden başlıyorsun. 3 maç boyunca bu ligden düşmezsin.", "You start at " + RankedLadder.Label(r.ToIdx, Lang) + ". You can't drop out of this league for 3 matches.")
                : r.Event == "galaxy" ? L("Usta I'in üstündeki ilk 100 oyuncunun arasındasın.", "You are among the top 100 players above Master I.")
                : RankedLadder.Label(r.FromIdx, Lang) + " → " + RankedLadder.Label(r.ToIdx, Lang);
            var ps = Paragraph(Text(sub, 16, "Baloo2-700", "#D6DCEE", 0, "ranked.res.pSub"), 1.45f); ps.style.maxWidth = 460; ps.style.marginTop = 8 + ps.style.marginTop.value.value; col.Add(ps); Up(ps, began, .35f);
            var go = Text(L("Devam", "Continue"), 17, "Montserrat-800", "#fff", 0, "ranked.res.close#2"); go.style.marginTop = 24; Pad(go, 14, 46, 14, 46);
            Raise(go, "linear-gradient(#5BE38A,#2BB45F)", 15, 5, "#1A7A40"); GalaxyFilters.HoverBrightness(go, 1.12f); Tap(go, () => CloseResult(false)); col.Add(go); Up(go, began, .45f);
        }

        /// <summary>rkUp .4s ease-out with a delay: translateY(16px), opacity 0 → none.</summary>
        private void Up(VisualElement e, float began, float delay)
        {
            e.style.opacity = 0;
            Enter(e, began, .4f, EaseOut, k => { e.style.opacity = k; e.style.translate = new Translate(0, 16 * (1 - k)); }, delay);
        }

        /// <summary>The promotion rays: 15 wedges of 8° every 24° from the top, faded by the radial mask (opaque to 10 %, clear at 62 % of the 509 px corner radius).</summary>
        private sealed class Rays : VisualElement
        {
            private readonly Color color;
            public Rays(Color color)
            {
                this.color = color; pickingMode = PickingMode.Ignore;
                generateVisualContent += Paint;
            }
            private void Paint(MeshGenerationContext context)
            {
                float size = contentRect.width; if (float.IsNaN(size) || size <= 0) return;
                var c = new Vector2(size / 2, size / 2); float corner = size / 2 * Mathf.Sqrt(2), r0 = corner * .10f, r1 = corner * .62f, edge = size / 2;
                const int bands = 24; var p = context.painter2D;
                for (int w = 0; w < 15; w++)
                {
                    float a0 = -90 + w * 24, a1 = a0 + 8;
                    for (int b = -1; b < bands; b++)
                    {
                        float inner = b < 0 ? 0 : r0 + (r1 - r0) * b / bands, outer = b < 0 ? r0 : r0 + (r1 - r0) * (b + 1) / bands;
                        if (inner >= edge) break; outer = Mathf.Min(outer, edge);
                        float alpha = b < 0 ? 1 : 1 - (b + .5f) / bands;
                        var fill = color; fill.a *= alpha; p.fillColor = fill;
                        p.BeginPath();
                        if (inner <= 0) p.MoveTo(c); else p.Arc(c, inner, Angle.Degrees(a1), Angle.Degrees(a0), ArcDirection.CounterClockwise);
                        p.Arc(c, outer, Angle.Degrees(a0), Angle.Degrees(a1));
                        p.ClosePath(); p.Fill();
                    }
                }
            }
        }
    }
}
