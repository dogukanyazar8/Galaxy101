using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    /// <summary>The top bar and the ranked home (G101-Dereceli.dc.html lines 32–200, renderVals()).</summary>
    public sealed partial class RankedScreen
    {
        // MODES (renderVals): name, colour, shade, players, tag, description.
        private (string Key, string Name, string Bg, string Shade, int Dots, string Tag, string Desc)[] Modes() => new[]
        {
            ("solo", "Solo", "#16B6A8", "#0C7F75", 1, L("4 OYUNCU", "4 PLAYERS"), L("Tek başına · seviyende 3 rakip", "Alone · 3 opponents at your level")),
            ("duo", "Duo", "#8B5CF6", "#5B34C2", 2, L("EŞLİ", "PAIRS"), L("Sen + 1 arkadaş · karşıda bir çift", "You + 1 friend vs a pair")),
            ("squad", "Squad", "#FF8A3D", "#C2561A", 4, L("4 ARKADAŞ", "4 FRIENDS"), L("4 arkadaş aynı dereceli masada", "4 friends on one ranked table")),
        };

        private string SeasonTag() => L("SEZON " + backend.Profile.Season + " · DERECELİ", "SEASON " + backend.Profile.Season + " · RANKED");
        private string SeasonSub()
        {
            var p = backend.Profile;
            return L("Sezon " + p.Season + " · " + p.DaysLeft + " gün kaldı · Solo / Duo / Squad", "Season " + p.Season + " · " + p.DaysLeft + " days left · Solo / Duo / Squad");
        }

        // ---------- top bar (86 px): back, badge, Dereceli + season, balance, Maç ara | İlerleme ----------
        private void BuildHeader()
        {
            var bar = Row(); MenuView.Place(bar, 0, 0, 1280, 86); Pad(bar, 0, 28, 0, 28); body.Add(bar);
            var back = Box("ranked.back"); Square(back, 46); Raise(back, "var(--pnH)", 14, 4, "var(--pnS)"); back.Add(view.Icon("v4/form-back", 20, "var(--ic)"));
            back.tooltip = L("Geri", "Back"); GalaxyFilters.HoverBrightness(back, 1.25f); Tap(back, Back); bar.Add(back);
            bar.Add(Badge());
            var titles = Column(); titles.style.marginLeft = 14; titles.style.minWidth = 0;
            titles.Add(Text(L("Dereceli", "Ranked"), 26, "Montserrat-700", "#F4F1E8", 1.1f, "ranked.head.title"));
            var sub = Text(SeasonSub(), 13, "Baloo2-700", "var(--mt)", 0, "ranked.head.sub"); sub.style.marginTop = 2; titles.Add(sub);
            bar.Add(titles); bar.Add(Grow());

            // Balance: --dp pill, padding 5 14 5 6, inset 0 2px 0 rgba(0,0,0,.3); 26 px coin; Montserrat 700 17 tabular.
            var chips = Row(); chips.style.marginLeft = 14; Pad(chips, 5, 14, 5, 6); view.Decorate(chips, "var(--dp)", 999); view.InsetRing(chips, 18)("rest");
            chips.Add(Coin(26, true));
            var amount = Tabular(Text(view.N(view.Model.Chips), 17, "Montserrat-700", "#F4F1E8", 0, "ranked.chipsLabel")); amount.style.marginLeft = 8; chips.Add(amount);
            bar.Add(chips);

            // views: Maç ara | İlerleme (padding 4, radius 14, --dp; option 9 16, radius 10, Montserrat 700 15).
            var views = Row(); views.style.marginLeft = 14; Pad(views, 4, 4, 4, 4); view.Decorate(views, "var(--dp)", 14);
            var keys = new[] { "home", "progress" }; var labels = new[] { L("Maç ara", "Find match"), L("İlerleme", "Progress") };
            for (int i = 0; i < 2; i++)
            {
                string key = keys[i]; bool on = View == key;
                var option = Text(labels[i], 15, "Montserrat-700", on ? "#F4F1E8" : "var(--mt)", 0, "ranked.v.pick[" + i + "]"); Pad(option, 9, 16, 9, 16);
                Raise(option, on ? "var(--pnH)" : "transparent", 10, on ? 3 : 0, "var(--pnS)"); if (i == 0) option.style.marginRight = 4;
                if (!on) { option.RegisterCallback<PointerEnterEvent>(_ => option.style.color = view.C("#F4F1E8")); option.RegisterCallback<PointerLeaveEvent>(_ => option.style.color = view.C("var(--mt)")); }
                Tap(option, () => SetView(key)); views.Add(option);
            }
            bar.Add(views);
        }

        /// <summary>
        /// The 52 px teal tile (radial-gradient(circle at 50% 30%,#3EE6D5,#16B6A8 55%,#0D8F84), 0 4px 0 #0C7F75, inset top
        /// highlight and a 1.5 px gold inset ring, top-half gloss) with the 44 px laurel-and-tile mark (rasterised, see ranked_art.py).
        /// </summary>
        private VisualElement Badge()
        {
            var badge = Box(); Square(badge, 52); badge.style.marginLeft = 14;
            Raise(badge, "radial-gradient(circle at 50% 30%,#3EE6D5,#16B6A8 55%,#0D8F84)", 16, 4, "#0C7F75");
            var gloss = Box(); gloss.style.position = Position.Absolute; gloss.style.left = gloss.style.right = gloss.style.top = 0; gloss.style.height = 26;
            view.Decorate(gloss, "linear-gradient(rgba(255,255,255,.2),rgba(255,255,255,0))", 0); gloss.style.borderTopLeftRadius = gloss.style.borderTopRightRadius = 16; badge.Add(gloss);
            var top = Fill(); Radius(top, 16); top.style.borderTopWidth = 1; top.style.borderTopColor = view.C("rgba(255,255,255,.4)"); badge.Add(top);
            InsetRing(badge, 1.5f, "rgba(255,217,100,.55)", 16);
            var art = Box(); art.style.width = art.style.height = 44; art.style.flexShrink = 0;
            var texture = Resources.Load<Texture2D>("Galaxy101/Ranked/badge@2x"); if (texture != null) art.style.backgroundImage = new StyleBackground(texture);
            badge.Add(art);
            return badge;
        }

        // ---------- home: 360 px league card | modes + "Masan" (left 28, right 28, top 96, bottom 26; gap 20) ----------
        private void BuildHome()
        {
            var p = backend.Profile;
            var home = Row(); MenuView.Place(home, 28, 96, 1224, 598); home.style.alignItems = Align.Stretch; body.Add(home);
            FadeIn(home, viewShownAt, .25f);
            home.Add(MeCard(p));
            var right = Column(); right.style.flexGrow = 1; right.style.flexShrink = 1; right.style.minWidth = 0; right.style.marginLeft = 20; home.Add(right);
            var modes = Row(); modes.style.height = 112; modes.style.alignItems = Align.Stretch; right.Add(modes);
            var defs = Modes();
            for (int i = 0; i < defs.Length; i++) { var card = ModeCard(i, defs[i]); if (i < defs.Length - 1) card.style.marginRight = 14; modes.Add(card); }
            right.Add(TablePanel(p));
        }

        /// <summary>animation: rkFade .25s both.</summary>
        private void FadeIn(VisualElement e, float began, float seconds) => Enter(e, began, seconds, Ease, k => e.style.opacity = k);

        private VisualElement MeCard(RankedProfile p)
        {
            var at = RankedLadder.At(p.Idx); bool gal = p.Idx >= RankedLadder.Steps; var league = at.League;
            var card = Column(); card.style.width = 360; card.style.alignItems = Align.Center; Pad(card, 22, 22, 20, 22); Raise(card, "var(--pn)", 24, 6, "var(--pnS)");
            // background: radial-gradient(ellipse at 50% 22%, me.glow, transparent 55%), var(--pn).
            var glow = Fill(); view.Decorate(glow, "radial-gradient(ellipse at 50% 22%," + league.Glow + "," + Fade(league.Glow, 0) + " 55%)", 24); card.Add(glow);

            var tag = Text(SeasonTag(), 11, "Baloo2-800", "var(--mt)", 0, "ranked.text.seasonTag"); Track(tag, 1.6f, 11); card.Add(tag);
            var emblem = Box(); emblem.style.width = emblem.style.height = 150; emblem.style.flexShrink = 0; emblem.style.marginTop = 10;
            emblem.Add(Emblem(league.Key, at.Tier, 150, gal ? p.SeasonRank : 0)); card.Add(emblem);
            Loop(emblem, t => emblem.style.translate = new Translate(0, FloatY(t, 4)));
            var label = Text(RankedLadder.Label(p.Idx, Lang), 30, "Montserrat-800", league.Text, 1.1f, "ranked.me.label"); label.style.marginTop = 10; card.Add(label);

            var progress = Column(); progress.style.alignSelf = Align.Stretch; progress.style.marginTop = 12;
            var track = Box(); track.style.height = 12; view.Decorate(track, "var(--dp)", 6);
            var fill = Box(); fill.style.height = Length.Percent(100); fill.style.width = Length.Percent(gal ? 100 : Mathf.Clamp(p.Lp, 0, 100)); view.Decorate(fill, league.Bar, 6); track.Add(fill);
            progress.Add(track);
            var lpRow = Row(); lpRow.style.justifyContent = Justify.SpaceBetween; lpRow.style.marginTop = 6;
            lpRow.Add(Text(gal ? p.Lp + " LP" : p.Lp + " / 100 LP", 13, "Baloo2-800", "#F4F1E8", 0, "ranked.me.lpText"));
            lpRow.Add(Text(NextText(p), 13, "Baloo2-800", "var(--mt)", 0, "ranked.me.next"));
            progress.Add(lpRow); card.Add(progress);

            if (p.Protected > 0 && p.Idx % 3 == 0)
            {
                var shield = Row(); shield.style.marginTop = 10; Pad(shield, 7, 12, 7, 12); view.Decorate(shield, "rgba(60,203,110,.14)", 12);
                shield.Add(view.Icon("v4/ranked-me-next", 15, "#8EF0B0"));
                var st = Text(L("Düşme koruması · " + p.Protected + " maç", "Demotion shield · " + p.Protected + " matches"), 13, "Baloo2-800", "#8EF0B0"); st.style.marginLeft = 8; shield.Add(st);
                card.Add(shield);
            }

            card.Add(Grow()); // margin-top: auto on the stats grid
            var stats = Row(); stats.style.alignSelf = Align.Stretch; stats.style.alignItems = Align.Stretch; stats.style.marginTop = 10;
            string rate = p.Games > 0 ? (En ? Mathf.RoundToInt(p.Top * 100f / p.Games) + "%" : "%" + Mathf.RoundToInt(p.Top * 100f / p.Games)) : "—";
            var values = new[] { (L("MAÇ", "MATCHES"), view.N(p.Games)), (L("1.LİK", "1ST PLACES"), view.N(p.Top)), (L("1.LİK ORANI", "TOP RATE"), rate) };
            for (int i = 0; i < values.Length; i++)
            {
                var cell = Column(); cell.style.flexGrow = 1; cell.style.flexBasis = 0; cell.style.alignItems = Align.Center; Pad(cell, 10, 4, 10, 4); view.Decorate(cell, "var(--sd)", 14);
                if (i < values.Length - 1) cell.style.marginRight = 8;
                cell.Add(Tabular(Text(values[i].Item2, 20, "Montserrat-800", "#F4F1E8", 0, "ranked.s.v[" + i + "]")));
                var k = Text(values[i].Item1, 11, "Baloo2-800", "var(--mt)", 0, "ranked.s.k[" + i + "]"); k.style.marginTop = 2; cell.Add(k);
                stats.Add(cell);
            }
            card.Add(stats);

            var go = Row("ranked.toProgress"); go.style.alignSelf = Align.Stretch; go.style.justifyContent = Justify.Center; go.style.marginTop = 10; Pad(go, 12, 0, 12, 0);
            Raise(go, "var(--pnH)", 14, 4, "var(--pnS)");
            go.Add(view.Icon("v4/ranked-to-progress", 17, "var(--ic)"));
            var gt = Text(L("Dereceli ilerlemem", "My ranked progress"), 15, "Montserrat-700", "#F4F1E8"); gt.style.marginLeft = 8; go.Add(gt);
            GalaxyFilters.HoverBrightness(go, 1.2f); Tap(go, () => SetView("progress")); card.Add(go);
            return card;
        }

        /// <summary>me.next: "Altın I'e 28 LP", "Galaksi'ye 6 LP" (Usta I), "İlk 100" (Galaksi).</summary>
        private string NextText(RankedProfile p)
        {
            if (p.Idx >= RankedLadder.Steps) return L("İlk 100", "Top 100");
            int left = 100 - p.Lp;
            if (p.Idx == 14) return L("Galaksi'ye " + left + " LP", left + " LP to Galaxy");
            string next = RankedLadder.Label(p.Idx + 1, Lang);
            return L(next + "'e " + left + " LP", left + " LP to " + next);
        }

        /// <summary>rkFloat (ease-in-out, 0 → −6 px → 0) over <paramref name="period"/> s.</summary>
        private static float FloatY(float t, float period, float delay = 0)
        {
            float p = Mathf.Repeat((t - delay) / period, 1);
            float k = p < .5f ? Bezier(.42f, 0, .58f, 1, p * 2) : 1 - Bezier(.42f, 0, .58f, 1, (p - .5f) * 2);
            return -6 * k;
        }

        private VisualElement ModeCard(int index, (string Key, string Name, string Bg, string Shade, int Dots, string Tag, string Desc) m)
        {
            bool on = Mode == m.Key;
            var slot = Box(); slot.style.flexGrow = 1; slot.style.flexBasis = 0; slot.style.flexShrink = 1; slot.style.minWidth = 0;
            var lift = Fill(); if (on) lift.style.translate = new Translate(0, -4); slot.Add(lift);
            if (on)
            {
                // box-shadow: 0 6px 0 shade, 0 3px 0 6px var(--bg3), 0 3px 0 9px #fff, 0 14px 30px <bg>88 (painted bottom-up).
                var glow = Fill(); view.Decorate(glow, m.Bg, 20); GalaxyFilters.Shadow(glow, 0, 14, 15, view.C(m.Bg + "88")); lift.Add(glow);
                var white = Box(); white.style.position = Position.Absolute; white.style.left = white.style.right = -9; white.style.top = -6; white.style.bottom = -12; view.Decorate(white, "#fff", 29); lift.Add(white);
                var dark = Box(); dark.style.position = Position.Absolute; dark.style.left = dark.style.right = -6; dark.style.top = -3; dark.style.bottom = -9; view.Decorate(dark, "var(--bg3)", 26); lift.Add(dark);
            }
            var card = Column("ranked.m.pick[" + index + "]"); Inset(card, 0); card.style.justifyContent = Justify.SpaceBetween; Pad(card, 14, 16, 14, 16);
            Raise(card, m.Bg, 20, 6, m.Shade);
            var gloss = Box(); gloss.style.position = Position.Absolute; gloss.style.left = gloss.style.right = gloss.style.top = 0; gloss.style.height = Length.Percent(50);
            view.Decorate(gloss, "linear-gradient(rgba(255,255,255,.18),rgba(255,255,255,0))", 0); gloss.style.borderTopLeftRadius = gloss.style.borderTopRightRadius = 20; card.Add(gloss);
            if (!on) GalaxyFilters.SaturationAndBrightness(card, .7f, .78f); // filter: saturate(.7) brightness(.78)

            var top = Row();
            for (int d = 0; d < m.Dots; d++) { var person = view.Icon("v4/ranked-m-pick", 22, "#fff"); if (d > 0) person.style.marginLeft = 6; top.Add(person); }
            top.Add(Grow());
            var tag = Text(m.Tag, 11, "Baloo2-800", "#fff", 0, "ranked.m.tag[" + index + "]"); Pad(tag, 3, 8, 3, 8); Track(tag, 1, 11); view.Decorate(tag, "rgba(0,0,0,.25)", 7); top.Add(tag);
            card.Add(top);
            var words = Column();
            words.Add(Text(m.Name, 24, "Montserrat-800", "#fff", 1.05f, "ranked.m.name[" + index + "]"));
            var desc = Clip(Text(m.Desc, 13, "Baloo2-700", "rgba(255,255,255,.92)", 1.3f, "ranked.m.desc[" + index + "]")); desc.style.marginTop = 3; words.Add(desc);
            card.Add(words);
            GalaxyFilters.HoverBrightness(card, 1.08f); string key = m.Key; Tap(card, () => SetMode(key));
            lift.Add(card);
            return slot;
        }

        // ---------- "Masan": seats, eşleşme aralığı, MAÇ ARA ----------
        private sealed class SeatView
        {
            public string Kind; // filled | invite | pending | opp | empty
            public string Tag, TagColor, Ring, RingColor;
            public RankedPlayer Player;
            public bool CanKick;
        }

        private List<SeatView> Seats(RankedProfile p)
        {
            string you = L("SEN", "YOU"), team = L("TAKIM", "TEAM"), rival = L("RAKİP", "RIVAL"), friend = L("ARKADAŞ", "FRIEND");
            var me = new RankedPlayer { Id = backend.MeId, Name = (string)view.Model.Profile["name"] ?? "Oyuncu", Color = (string)view.Model.Profile["color"] ?? "#FF8A5B", Idx = p.Idx, Me = true };
            var seats = new List<SeatView> { new SeatView { Kind = "filled", Player = me, Tag = Mode == "duo" ? team : you, TagColor = "var(--a1)", RingColor = "var(--a1)", Ring = "var(--a1)" } };
            SeatView FriendSeat(int i)
            {
                var m = i < party.Count ? party[i] : null;
                if (m == null) return new SeatView { Kind = Busy ? "empty" : "invite", Tag = friend, TagColor = "var(--mt)", Ring = "rgba(255,255,255,.06)" };
                if (!m.In) return new SeatView { Kind = "pending", Player = m.Friend, Tag = friend, TagColor = "var(--mt)" };
                return new SeatView { Kind = "filled", Player = m.Friend, Tag = Mode == "duo" ? team : friend, TagColor = "#8EF0B0", RingColor = "#3CCB6E", CanKick = !Busy };
            }
            var opps = Searching ? search?.Opponents : null;
            SeatView OppSeat(int i)
            {
                if (opps != null && i < opps.Count) return new SeatView { Kind = "filled", Player = opps[i], Tag = rival, TagColor = "#FF8A94", RingColor = "#FF5E6C" };
                return new SeatView { Kind = "opp", Tag = rival, TagColor = "var(--mt)" };
            }
            if (Mode == "solo") seats.AddRange(new[] { OppSeat(0), OppSeat(1), OppSeat(2) });
            if (Mode == "duo") seats.AddRange(new[] { FriendSeat(0), OppSeat(0), OppSeat(1) });
            if (Mode == "squad") seats.AddRange(new[] { FriendSeat(0), FriendSeat(1), FriendSeat(2) });
            return seats;
        }

        /// <summary>range: the league window (±1, ±2 after 30 s; Squad: the party's ±1).</summary>
        private (string Label, string Short, string Hint) Range(RankedProfile p)
        {
            var lg = RankedLadder.Leagues; int me = RankedLadder.LeagueOf(p.Idx);
            int lo = Math.Max(0, me - 1), hi = Math.Min(5, me + 1), lo2 = Math.Max(0, me - 2), hi2 = Math.Min(5, me + 2);
            bool wide = Searching && search != null && search.Range >= 2;
            if (Mode == "squad")
                return (L("Takım aralığı · ±1 lig", "Party range · ±1 league"), "",
                    L(lg[lo].Tr + " – " + lg[hi].Tr + " arası arkadaşlar katılabilir · herkes kendi sırasına göre LP kazanır", "Friends from " + lg[lo].En + " to " + lg[hi].En + " can join · everyone earns LP for their own place"));
            string span = wide ? lg[lo2].Name(Lang) + " – " + lg[hi2].Name(Lang) : lg[lo].Name(Lang) + " – " + lg[hi].Name(Lang);
            return (L("Eşleşme aralığı · ", "Match range · ") + span + (wide ? " (±2)" : " (±1)"), span,
                wide ? L("30 sn geçti · arama ±2 lige genişledi", "Search widened to ±2 leagues after 30 s") : L("Seviyene uygun rakipler · 30 sn sonra ±2 lige genişler", "Opponents at your level · widens to ±2 leagues after 30 s"));
        }

        private VisualElement TablePanel(RankedProfile p)
        {
            var panel = Column(); panel.style.flexGrow = 1; panel.style.flexShrink = 1; panel.style.minHeight = 0; panel.style.marginTop = 16; Pad(panel, 18, 20, 18, 20);
            Raise(panel, "var(--pn)", 24, 6, "var(--pnS)");
            var head = Row();
            string title = Mode == "squad" ? L("Squad masan", "Your squad") : Mode == "duo" ? L("Duo masan", "Your duo") : L("Masan", "Your table");
            string sub = Mode == "squad" ? (1 + Joined) + "/4" : Mode == "duo" ? L("Eş ", "Partner ") + Joined + "/1" : "";
            head.Add(Text(title, 19, "Montserrat-700", "#F4F1E8", 0, "ranked.table.title"));
            var subLabel = Text(sub, 13, "Baloo2-700", "var(--mt)", 0, "ranked.table.sub"); subLabel.style.marginLeft = 10; if (sub.Length == 0) subLabel.style.height = 0; head.Add(subLabel);
            head.Add(Grow());
            var rules = Row(); Pad(rules, 6, 12, 6, 12); view.Decorate(rules, "var(--sd)", 11);
            rules.Add(view.Icon("v4/ranked-table-sub", 14, "var(--mt)"));
            var rt = Text(L("101 · 5 el · 30 sn · katlamasız", "101 · 5 hands · 30 s · no doubling"), 12, "Baloo2-800", "var(--mt)"); rt.style.marginLeft = 8; rules.Add(rt); // R-94
            head.Add(rules); panel.Add(head);

            var range = Range(p);
            var row = Row(); row.style.alignItems = Align.Stretch; row.style.flexGrow = 1; row.style.flexShrink = 1; row.style.minHeight = 0; row.style.marginTop = 14;
            var seats = Seats(p);
            for (int i = 0; i < seats.Count; i++) { var seat = Seat(i, seats[i], range.Short); if (i < seats.Count - 1) seat.style.marginRight = 12; row.Add(seat); }
            panel.Add(row);

            var foot = Row(); foot.style.marginTop = 14;
            var box = Row(); box.style.flexGrow = 1; box.style.flexShrink = 1; box.style.minWidth = 0; Pad(box, 10, 14, 10, 14); view.Decorate(box, "var(--sd)", 14);
            box.Add(view.Icon("v4/ranked-range-short", 18, "var(--a1)"));
            var words = Column(); words.style.marginLeft = 10; words.style.flexShrink = 1; words.style.minWidth = 0;
            words.Add(Text(range.Label, 14, "Montserrat-800", "#F4F1E8", 0, "ranked.range.label"));
            var hint = Clip(Text(range.Hint, 12, "Baloo2-700", "var(--mt)", 0, "ranked.range.hint")); hint.style.marginTop = 1; words.Add(hint);
            box.Add(words); foot.Add(box);
            foot.Add(Searching ? SearchBox() : Cta());
            panel.Add(foot);
            return panel;
        }

        private VisualElement Seat(int i, SeatView z, string rangeShort)
        {
            var seat = Column(); seat.style.flexGrow = 1; seat.style.flexBasis = 0; seat.style.flexShrink = 1; seat.style.minWidth = 0;
            seat.style.alignItems = Align.Center; seat.style.justifyContent = Justify.Center; Pad(seat, 14, 8, 14, 8); view.Decorate(seat, "var(--sd)", 18);
            if (z.Ring != null) InsetRing(seat, 2, z.Ring, 18);
            var tag = Text(z.Tag, 10, "Baloo2-800", z.TagColor, 0, "ranked.z.tag[" + i + "]"); Track(tag, 1, 10);
            tag.style.position = Position.Absolute; tag.style.left = 10; tag.style.top = 9; seat.Add(tag);
            if (z.CanKick)
            {
                var kick = Box("ranked.z.kick[" + i + "]"); kick.style.position = Position.Absolute; kick.style.right = 8; kick.style.top = 8; Square(kick, 26); Radius(kick, 9);
                kick.Add(view.Icon("v4/menu-close-settings", 12, "var(--mt)")); kick.tooltip = L("Takımdan çıkar", "Remove from party");
                kick.RegisterCallback<PointerEnterEvent>(_ => kick.style.backgroundColor = view.C("var(--pnH)")); kick.RegisterCallback<PointerLeaveEvent>(_ => kick.style.backgroundColor = Color.clear);
                string id = z.Player.Id; Tap(kick, () => Kick(id)); seat.Add(kick);
            }
            switch (z.Kind)
            {
                case "filled":
                {
                    var p = z.Player; var at = RankedLadder.At(p.Idx);
                    var col = Column(); col.style.alignSelf = Align.Stretch; col.style.alignItems = Align.Center;
                    var ini = Disc(64, p.Color, Initials(p.Name), 22, "ranked.z.ini[" + i + "]");
                    // box-shadow: 0 0 0 3px var(--sd), 0 0 0 5px ringC — the outer band first, the inner one over it.
                    GalaxyFilters.SpreadRing(ini, 3)(3, view.C("var(--sd)")); GalaxyFilters.SpreadRing(ini, 5)(5, view.C(z.RingColor));
                    col.Add(ini);
                    var name = Clip(Text(p.Name, 16, "Montserrat-700", "#F4F1E8", 1.2f, "ranked.z.name[" + i + "]")); name.style.marginTop = 7; name.style.maxWidth = Length.Percent(100); col.Add(name);
                    var league = Row(); league.style.marginTop = 7;
                    league.Add(Emblem(at.League.Key, at.Tier, 24));
                    var lt = Text(RankedLadder.Label(p.Idx, Lang), 12, "Baloo2-800", at.League.Text); lt.style.marginLeft = 5; league.Add(lt);
                    col.Add(league); seat.Add(col);
                    SeatIn(col, "seat:" + p.Id);
                    break;
                }
                case "invite":
                {
                    var col = Column("ranked.openInvite[" + i + "]"); col.style.alignItems = Align.Center;
                    var circle = Box(); Square(circle, 64); Radius(circle, 32); DashedBorder(circle, 2, view.C("var(--a1)"));
                    circle.Add(view.Icon("v4/lobby-open-create", 24, "var(--a1)")); col.Add(circle);
                    col.RegisterCallback<PointerEnterEvent>(_ => circle.style.backgroundColor = new Color(1, 1, 1, .06f)); col.RegisterCallback<PointerLeaveEvent>(_ => circle.style.backgroundColor = Color.clear);
                    var it = Text(L("Davet et", "Invite"), 15, "Montserrat-700", "var(--a1)", 0, "ranked.text.invite[" + i + "]"); it.style.marginTop = 9; col.Add(it);
                    var fs = Text(L("Arkadaş yeri", "Friend slot"), 12, "Baloo2-700", "var(--mt)", 0, "ranked.text.friendSlot[" + i + "]"); fs.style.marginTop = 9; col.Add(fs);
                    Tap(col, OpenInvite); seat.Add(col);
                    break;
                }
                case "pending":
                {
                    var p = z.Player; var col = Column(); col.style.alignItems = Align.Center;
                    var ini = Disc(64, p.Color, Initials(p.Name), 22, "ranked.z.ini[" + i + "]"); ini.style.opacity = .55f;
                    var spin = Spinner(74, 3, "var(--a1)", .9f); spin.style.position = Position.Absolute; spin.style.left = spin.style.top = -5; ini.Add(spin);
                    col.Add(ini);
                    var name = Text(p.Name, 15, "Montserrat-700", "#F4F1E8", 0, "ranked.z.name[" + i + "]"); name.style.marginTop = 9; col.Add(name);
                    var wait = Text(L("Davet bekleniyor", "Waiting for reply"), 12, "Baloo2-700", "var(--mt)", 0, "ranked.text.pending[" + i + "]"); wait.style.marginTop = 9; col.Add(wait);
                    seat.Add(col);
                    break;
                }
                case "opp":
                {
                    var col = Column(); col.style.alignItems = Align.Center;
                    var q = Disc(64, "var(--dp)", null, 0); var mark = Text("?", 26, "Montserrat-800", "var(--mt)"); mark.style.unityTextAlign = TextAnchor.MiddleCenter; q.Add(mark);
                    if (Searching) { var spin = Spinner(74, 3, "var(--a1)", 1.1f); spin.style.position = Position.Absolute; spin.style.left = spin.style.top = -5; q.Add(spin); }
                    col.Add(q);
                    var ol = Text(L("Rakip", "Opponent"), 15, "Montserrat-700", "var(--mt)", 0, "ranked.z.oppLabel[" + i + "]"); ol.style.marginTop = 9; col.Add(ol);
                    var rs = Text(rangeShort, 12, "Baloo2-700", "var(--mt)", 0, "ranked.range.short[" + i + "]"); rs.style.marginTop = 9; col.Add(rs);
                    seat.Add(col);
                    break;
                }
            }
            return seat;
        }

        /// <summary>rkSeat .45s cubic-bezier(.2,1.3,.4,1): 0 % opacity 0 scale .6 → 70 % opacity 1 scale 1.08 → 100 % scale 1 (once per player).</summary>
        private void SeatIn(VisualElement e, string key)
        {
            if (!seatsSeen.TryGetValue(key, out float began)) seatsSeen[key] = began = Time.realtimeSinceStartup;
            Enter(e, began, .45f, Linear, u => Pop(e, u, .7f, .6f, 1.08f, 0, 0, .2f, 1.3f, .4f, 1));
        }

        /// <summary>A three-keyframe pop (rkSeat, rkPop): the timing function eases each keyframe interval on its own.</summary>
        private static void Pop(VisualElement e, float u, float mid, float fromScale, float midScale, float fromRotate, float midRotate, float x1, float y1, float x2, float y2)
        {
            float s, r, o;
            if (u < mid) { float k = Bezier(x1, y1, x2, y2, u / mid); s = Mathf.LerpUnclamped(fromScale, midScale, k); r = Mathf.LerpUnclamped(fromRotate, midRotate, k); o = Mathf.Clamp01(k); }
            else { float k = Bezier(x1, y1, x2, y2, (u - mid) / (1 - mid)); s = Mathf.LerpUnclamped(midScale, 1, k); r = Mathf.LerpUnclamped(midRotate, 0, k); o = 1; }
            e.style.scale = new Scale(Vector2.one * s); e.style.rotate = new Rotate(r); e.style.opacity = o;
        }

        private VisualElement Cta()
        {
            bool ready = Ready; int need = Need(), joined = Joined;
            string why = ready ? "" : Mode == "duo" ? L("Duo için 1 arkadaş davet et", "Invite 1 friend for Duo")
                : L("Squad için " + (need - joined) + " arkadaş daha davet et", "Invite " + (need - joined) + " more friend" + (need - joined > 1 ? "s" : "") + " for Squad");
            string label = Mode == "squad" ? L("MASAYI KUR", "START TABLE") : L("MAÇ ARA", "FIND MATCH");
            string sub = ready ? (Mode == "squad" ? L("4 arkadaş · dereceli", "4 friends · ranked") : L("Dereceli · 101 · 5 el", "Ranked · 101 · 5 hands")) : why;
            string fg = ready ? "var(--aT)" : "var(--mt)";
            var go = Column("ranked.cta.go"); go.style.width = 300; go.style.height = 64; go.style.marginLeft = 14; go.style.alignItems = Align.Center; go.style.justifyContent = Justify.Center;
            Raise(go, ready ? "linear-gradient(var(--a1),var(--a2))" : "var(--pnH)", 18, 6, ready ? "var(--aS)" : "var(--pnS)");
            var title = Text(label, 24, "Montserrat-800", fg, 1, "ranked.cta.label"); Track(title, .5f, 24); go.Add(title);
            var line = Text(sub, 12, "Baloo2-800", fg, 0, "ranked.cta.sub"); line.style.marginTop = 3; line.style.opacity = .85f; go.Add(line);
            if (ready) Pulse(go);
            go.tooltip = why; GalaxyFilters.HoverBrightness(go, 1.08f);
            Tap(go, () => { if (!Ready) { if (Need() > 0) OpenInvite(); return; } StartSearch(); });
            return go;
        }

        /// <summary>rkPulse 2.2s infinite (ease per half): 0 0 0 0 var(--gl) → 0 0 0 14px transparent → back.</summary>
        private void Pulse(VisualElement element)
        {
            var ring = GalaxyFilters.SpreadRing(element, 14); var glow = view.C("var(--gl)");
            Loop(element, t =>
            {
                float p = Mathf.Repeat(t / 2.2f, 1), f = Ease(p < .5f ? p * 2 : (p - .5f) * 2), spread = p < .5f ? f : 1 - f;
                var c = glow; c.a *= 1 - spread; ring(14 * spread, c);
            });
        }

        private VisualElement SearchBox()
        {
            var box = Row(); box.style.width = 300; box.style.height = 64; box.style.marginLeft = 14; Pad(box, 0, 8, 0, 18); view.Decorate(box, "var(--sd)", 18);
            box.Add(Spinner(22, 3, "var(--a1)", .8f));
            var words = Column(); words.style.flexGrow = 1; words.style.flexShrink = 1; words.style.minWidth = 0; words.style.marginLeft = 12;
            words.Add(Text(L("Rakip aranıyor", "Finding opponents"), 16, "Montserrat-800", "#F4F1E8", 0, "ranked.text.searching"));
            clockLabel = Text(ClockText(), 13, "Baloo2-800", "var(--a1)", 0, "ranked.clock"); clockLabel.userData = clockLabel.text; Tabular(clockLabel); words.Add(clockLabel);
            box.Add(words);
            var cancel = Text(L("İptal", "Cancel"), 15, "Montserrat-700", "#F4F1E8", 0, "ranked.cancel"); cancel.style.marginLeft = 12; Pad(cancel, 11, 16, 11, 16);
            Raise(cancel, "var(--pnH)", 13, 4, "var(--pnS)"); GalaxyFilters.HoverBrightness(cancel, 1.2f); Tap(cancel, Cancel); box.Add(cancel);
            InsetRing(box, 2, "var(--a1)", 18);
            return box;
        }
    }
}
