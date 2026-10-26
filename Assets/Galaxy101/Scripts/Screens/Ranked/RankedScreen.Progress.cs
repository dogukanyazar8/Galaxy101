using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    /// <summary>İlerleme (G101-Dereceli.dc.html lines 202–251): league road, season rewards, recent matches.</summary>
    public sealed partial class RankedScreen
    {
        private void BuildProgress()
        {
            var p = backend.Profile; var lg = RankedLadder.Leagues;
            int here = RankedLadder.LeagueOf(p.Idx), peak = RankedLadder.LeagueOf(p.Peak > 0 ? p.Peak : p.Idx);
            // grid: columns minmax(0,1fr) 340px, rows auto minmax(0,1fr), gap 18 (left 28, right 28, top 96, bottom 26).
            var page = Column(); MenuView.Place(page, 28, 96, 1224, 598); body.Add(page); FadeIn(page, viewShownAt, .25f);

            // Lig yolu: six leagues, the tiers inside each as pills.
            var road = Column(); Pad(road, 18, 22, 20, 22); Raise(road, "var(--pn)", 24, 6, "var(--pnS)");
            var head = Row();
            head.Add(Text(L("Lig yolu", "League road"), 19, "Montserrat-700", "#F4F1E8", 0, "ranked.text.road"));
            var rs = Text(L("16 basamak · her kademe 100 LP", "16 steps · 100 LP per tier"), 13, "Baloo2-700", "var(--mt)", 0, "ranked.text.roadSub"); rs.style.marginLeft = 10; head.Add(rs);
            road.Add(head);
            var steps = Row(); steps.style.alignItems = Align.Stretch; steps.style.marginTop = 14;
            for (int i = 0; i < lg.Count; i++)
            {
                var l = lg[i]; bool isHere = here == i, reached = peak >= i;
                var slot = Box(); slot.style.flexGrow = 1; slot.style.flexBasis = 0; slot.style.minWidth = 0; if (i < lg.Count - 1) slot.style.marginRight = 10;
                if (isHere) { var halo = Fill(); view.Decorate(halo, "var(--pnH)", 18); GalaxyFilters.Shadow(halo, 0, 0, 12, view.C(l.Glow)); slot.Add(halo); } // 0 0 24px glow
                var item = Column(); item.style.flexGrow = 1; item.style.alignItems = Align.Center; Pad(item, 14, 8, 12, 8); view.Decorate(item, isHere ? "var(--pnH)" : "var(--sd)", 18);
                item.style.opacity = reached || isHere ? 1 : .55f;
                if (isHere) InsetRing(item, 2, l.Text, 18);
                if (isHere)
                {
                    var you = Text(L("BURADASIN", "YOU ARE HERE"), 10, "Baloo2-800", "var(--aT)", 0, "ranked.text.youHere[" + i + "]"); MenuView.Tracking(you, 1, 10); Pad(you, 3, 10, 3, 10); view.Decorate(you, "var(--a1)", 8);
                    you.style.position = Position.Absolute; you.style.top = -11; you.style.left = Length.Percent(50); you.style.translate = new Translate(Length.Percent(-50), 0); item.Add(you);
                }
                var emblem = Emblem(l.Key, 0, 72); if (!reached) GalaxyFilters.SaturationAndBrightness(emblem, .3f, .7f); item.Add(emblem); // grayscale(.7) brightness(.7)
                var name = Text(l.Name(Lang), 16, "Montserrat-800", reached ? l.Text : "var(--mt)", 0, "ranked.r.name[" + i + "]"); name.style.marginTop = 8; item.Add(name);
                var pills = Row(); pills.style.marginTop = 8;
                if (i == 5)
                {
                    bool gx = p.Idx >= RankedLadder.Steps;
                    pills.Add(StepPill(0, "#1–100", gx ? "#8C6EFF" : "var(--dp)", gx ? "#fff" : "var(--mt)", null, ""));
                }
                else
                    for (int t = 3, k = 0; t >= 1; t--, k++)
                    {
                        int s = i * 3 + (3 - t); bool cur = s == p.Idx, done = s < p.Idx;
                        var pill = StepPill(k, RankedLadder.Roman[t], cur ? l.Text : done ? "rgba(255,255,255,.14)" : "var(--dp)", cur ? "#1A1206" : done ? "#F4F1E8" : "var(--mt)", cur ? l.Glow : null, RankedLadder.Label(s, Lang));
                        if (k > 0) pill.style.marginLeft = 5; pills.Add(pill);
                    }
                item.Add(pills);
                slot.Add(item); steps.Add(slot);
            }
            road.Add(steps); page.Add(road);

            var lower = Row(); lower.style.alignItems = Align.Stretch; lower.style.flexGrow = 1; lower.style.flexShrink = 1; lower.style.minHeight = 0; lower.style.marginTop = 18; page.Add(lower);

            // Sezon ödülleri: the highest league reached this season decides the reward.
            var rewards = Column(); rewards.style.flexGrow = 1; rewards.style.flexShrink = 1; rewards.style.minWidth = 0; Pad(rewards, 18, 22, 18, 22); Raise(rewards, "var(--pn)", 24, 6, "var(--pnS)");
            var rh = Row();
            rh.Add(Text(L("Sezon ödülleri", "Season rewards"), 19, "Montserrat-700", "#F4F1E8", 0, "ranked.text.rewards"));
            var rsub = Clip(Text(L("Sezonda ulaştığın en yüksek lige göre · çip, avatar çerçevesi ve profil rozeti", "Based on the highest league you reach · chips, avatar frame and profile badge"), 13, "Baloo2-700", "var(--mt)", 0, "ranked.text.rewardsSub"));
            rsub.style.marginLeft = 10; rh.Add(rsub); rewards.Add(rh);
            var grid = Row(); grid.style.alignItems = Align.Stretch; grid.style.flexGrow = 1; grid.style.marginTop = 12;
            string initials = Initials((string)view.Model.Profile["name"] ?? "Oyuncu"), color = (string)view.Model.Profile["color"] ?? "#FF8A5B";
            for (int i = 0; i < lg.Count; i++)
            {
                var l = lg[i]; bool mine = i == peak;
                var cell = Column(); cell.style.flexGrow = 1; cell.style.flexBasis = 0; cell.style.minWidth = 0; cell.style.alignItems = Align.Center; cell.style.justifyContent = Justify.Center;
                Pad(cell, 12, 6, 12, 6); view.Decorate(cell, mine ? "var(--pnH)" : "var(--sd)", 18); if (mine) InsetRing(cell, 2, "var(--a1)", 18);
                if (i < lg.Count - 1) cell.style.marginRight = 10;
                var avatar = FramedAvatar(l.Key, 64, initials, color); avatar.style.opacity = i <= peak ? 1 : .5f; cell.Add(avatar);
                var chips = Row(); chips.style.marginTop = 8; chips.Add(Coin(16, false));
                var amount = Tabular(Text(view.N(l.RewardChips), 14, "Montserrat-800", "#F4F1E8", 0, "ranked.w.chips[" + i + "]")); amount.style.marginLeft = 5; chips.Add(amount); cell.Add(chips);
                string st = i < peak ? L("Ulaşıldı", "Reached") : mine ? L("Kazanacağın", "You get this") : L("Kilitli", "Locked");
                var stl = Text(st, 11, "Baloo2-800", mine ? "var(--a1)" : i < peak ? "#8EF0B0" : "var(--mt)", 0, "ranked.w.st[" + i + "]"); stl.style.marginTop = 8; cell.Add(stl);
                grid.Add(cell);
            }
            rewards.Add(grid); lower.Add(rewards);

            // Son maçlar + the LP rule of the current tier.
            var hist = Column(); hist.style.width = 340; hist.style.flexShrink = 0; hist.style.marginLeft = 18; Pad(hist, 18, 20, 18, 20); Raise(hist, "var(--pn)", 24, 6, "var(--pnS)");
            var hh = Row(); hh.style.justifyContent = Justify.SpaceBetween;
            hh.Add(Text(L("Son maçlar", "Recent matches"), 19, "Montserrat-700", "#F4F1E8", 0, "ranked.text.recent"));
            var ds = RankedLadder.Deltas(Math.Min(p.Idx, 14));
            hh.Add(Text(string.Join(" · ", ds.Select((d, i) => (i + 1) + ". " + (d > 0 ? "+" : "−") + Math.Abs(d))), 12, "Baloo2-800", "var(--mt)", 0, "ranked.lpRule"));
            hist.Add(hh);
            var rows = p.History.Take(12).ToList();
            if (rows.Count == 0)
            {
                var empty = Column(); empty.style.flexGrow = 1; empty.style.justifyContent = Justify.Center; empty.style.marginTop = 10; Pad(empty, 0, 10, 0, 10);
                var none = Paragraph(Text(L("Henüz dereceli maç oynamadın. İlk maçını bitirince sonuçların burada görünür.", "No ranked matches yet. Your results show up here after your first match."), 14, "Baloo2-700", "var(--mt)", 0, "ranked.text.noHist"), 1.45f);
                empty.Add(none); hist.Add(empty);
            }
            else
            {
                var scroll = view.Scroll(false); scroll.name = "ranked-history"; scroll.style.flexGrow = 1; scroll.style.marginTop = 10; scroll.pickingMode = PickingMode.Position;
                var names = new System.Collections.Generic.Dictionary<string, string> { ["solo"] = "Solo", ["duo"] = "Duo", ["squad"] = "Squad" };
                for (int i = 0; i < rows.Count; i++)
                {
                    var h = rows[i];
                    var row = Row(); Pad(row, 8, 10, 8, 10); view.Decorate(row, "var(--sd)", 12); if (i > 0) row.style.marginTop = 6;
                    var place = Text(h.Place + ".", 14, "Montserrat-800", h.Place == 1 ? "#3A2400" : "#F4F1E8", 0, "ranked.h.place[" + i + "]"); Square(place, 30); place.style.unityTextAlign = TextAnchor.MiddleCenter;
                    view.Decorate(place, h.Place == 1 ? "linear-gradient(#FFE58A,#E8A317)" : "var(--pnH)", 10); row.Add(place);
                    var words = Column(); words.style.flexGrow = 1; words.style.flexShrink = 1; words.style.minWidth = 0; words.style.marginLeft = 10;
                    words.Add(Text(L("Dereceli · ", "Ranked · ") + (names.TryGetValue(h.Mode ?? "solo", out var mn) ? mn : "Solo"), 14, "Montserrat-700", "#F4F1E8", 0, "ranked.h.mode[" + i + "]"));
                    words.Add(Text(RankedLadder.Label(h.Idx, Lang), 11, "Baloo2-700", "var(--mt)", 0, "ranked.h.league[" + i + "]"));
                    row.Add(words);
                    var d = Tabular(Text((h.Delta > 0 ? "+" : "−") + Math.Abs(h.Delta) + " LP", 15, "Montserrat-800", h.Delta > 0 ? "#5BE38A" : "#FF8A94", 0, "ranked.h.d[" + i + "]")); d.style.marginLeft = 10; row.Add(d);
                    scroll.Add(row);
                }
                hist.Add(scroll);
            }
            lower.Add(hist);
        }

        private VisualElement StepPill(int k, string label, string bg, string fg, string glow, string title)
        {
            var pill = Text(label, 11, "Baloo2-800", fg, 0, "ranked.p.label[" + k + "]"); Pad(pill, 3, 8, 3, 8); view.Decorate(pill, bg, 7);
            if (glow != null) GalaxyFilters.Shadow(pill, 0, 0, 5, view.C(glow)); // 0 0 10px glow
            if (!string.IsNullOrEmpty(title)) pill.tooltip = title;
            return pill;
        }
    }
}
