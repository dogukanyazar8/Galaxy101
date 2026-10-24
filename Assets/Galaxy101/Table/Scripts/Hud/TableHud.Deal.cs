using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101.Table
{
    public sealed partial class TableHud
    {
        /// <summary>The deal card (spec/21 dealHud): who deals, the step, the two dice, then indicator → okey.</summary>
        public sealed class DealInfoCard
        {
            public int Dealer, DieA, DieB, LastDie = -1, Roll = -1;
            public string Step, Indicator, Okey;
            public bool Fake, Revealed;
            public float StepAt;
        }

        private string DealStepText(DealInfoCard d)
        {
            string dealer = d.Dealer == 0 ? null : FirstName(d.Dealer), starter = (d.Dealer + 1) % 4 == 0 ? null : FirstName((d.Dealer + 1) % 4);
            switch (d.Step)
            {
                case "stack": return L("106 taş 7’li balyalara diziliyor", "Stacking 106 tiles into piles of 7");
                case "roll": return d.Dealer == 0 ? L("Zarı iki kez at", "Roll the die twice") : L(dealer + " zar atıyor", dealer + " is rolling");
                case "rolling": return L("Zar yuvarlanıyor…", "Rolling…");
                case "indicator": return d.Fake ? L("Sahte okey çıktı · üstündeki taş gösterge oldu", "Fake okey turned up · the tile above is the indicator") : L(d.DieA + ". balya · alttan " + d.DieB + ". taş gösterge", "Pile " + d.DieA + " · tile " + d.DieB + " from the bottom is the indicator");
                case "extra": return L("Fazla taş yandaki balyanın üstüne", "Extra tile onto the next pile");
                case "deal": return L("Balyalar dağıtılıyor", "Handing out the piles");
                case "centre": return L("Kalan balyalar ortaya", "Remaining piles to the centre");
                default: return starter == null ? L("22 taşla sen başlıyorsun", "You start with 22 tiles") : L(starter + " 22 taşla başlıyor", starter + " starts with 22 tiles");
            }
        }

        /// <summary>
        /// G101-Masa dealHud, top 84: a glass card (padding 10 20 10 10, gap 16, radius 22, dhIn .55 s) — the dealer's disc
        /// with the DAĞITAN tag, title and step (dhSub on each change), | the two dice over ZAR, | indicator → okey
        /// (dhTile turns). While I deal, "Zar at" waits right of the felt (right 64, bottom 250) and bobs.
        /// </summary>
        private void BuildDeal(DealInfoCard d)
        {
            var row = FullRow(84);
            var card = Row(16); card.name = "deal-card"; Pad(card, 10, 20, 10, 10);
            view.Decorate(card, "linear-gradient(180deg,rgba(22,26,62,.9),rgba(8,10,30,.9))", 22); Border(card, 1, new Color(1, 1, 1, .1f));
            GalaxyFilters.BackdropBlur(card, 12); GalaxyFilters.Shadow(card, 0, 18, 22, new Color(0, 0, 0, .45f));
            // inset 0 1px 0 rgba(255,255,255,.08)
            var sheen = new VisualElement { pickingMode = PickingMode.Ignore }; sheen.style.position = Position.Absolute; sheen.style.left = sheen.style.right = sheen.style.top = sheen.style.bottom = 0;
            sheen.style.borderTopLeftRadius = sheen.style.borderTopRightRadius = sheen.style.borderBottomLeftRadius = sheen.style.borderBottomRightRadius = 21;
            sheen.style.borderTopWidth = 1; sheen.style.borderTopColor = new Color(1, 1, 1, .08f); card.Add(sheen);
            float shown = Since("deal-card");
            motions.Add(t => { float k = MenuView.CssBezier(.22f, 1, .36f, 1, Mathf.Clamp01((t - shown) / .55f)); card.style.opacity = Mathf.Clamp01(k); card.style.translate = new Translate(0, -14 * (1 - k)); card.style.scale = new Scale(Vector2.one * (.96f + .04f * k)); });

            // The dealer: a 46 px disc in the seat colour, 2 px white ring, initials 700 16px; DAĞITAN at right −5, bottom −4.
            var seat = Seat(d.Dealer);
            var who = new VisualElement { pickingMode = PickingMode.Ignore }; who.style.width = who.style.height = 46; who.style.flexShrink = 0;
            var disc = new VisualElement { pickingMode = PickingMode.Ignore }; MenuView.Place(disc, 0, 0, 46, 46); disc.style.alignItems = Align.Center; disc.style.justifyContent = Justify.Center;
            view.Decorate(disc, (string)seat["color"], 23); Border(disc, 2, new Color(1, 1, 1, .85f)); disc.Add(Text((string)seat["ini"], "Montserrat-700", 16, "#fff")); who.Add(disc);
            var badge = Text(L("DAĞITAN", "DEALER"), "Baloo2-800", 9, "var(--aT)"); MenuView.Tracking(badge, .8f, 9);
            badge.style.position = Position.Absolute; badge.style.right = -5; badge.style.bottom = -4; badge.style.height = 18; badge.style.whiteSpace = WhiteSpace.NoWrap; badge.style.flexShrink = 0; badge.style.unityTextAlign = TextAnchor.MiddleCenter; Pad(badge, 0, 6, 0, 6);
            view.Decorate(badge, "linear-gradient(var(--a1),var(--a2))", 9, 2, "var(--aS)"); who.Add(badge); card.Add(who);

            var words = new VisualElement { pickingMode = PickingMode.Ignore }; words.style.minWidth = 220; words.style.justifyContent = Justify.Center;
            var title = Text(d.Dealer == 0 ? L("Taşları sen dağıtıyorsun", "You are dealing") : L("Taşları " + FirstName(d.Dealer) + " dağıtıyor", FirstName(d.Dealer) + " is dealing"), "Montserrat-700", 18, "#F4F1E8");
            title.style.height = 18 * 1.1f; title.style.unityTextAlign = TextAnchor.MiddleLeft; words.Add(title);
            var step = Text(DealStepText(d), "Baloo2-700", 12, "var(--ic)"); step.name = "deal-step"; step.style.height = 12 * 1.3f; step.style.unityTextAlign = TextAnchor.MiddleLeft; step.style.marginTop = 3; words.Add(step);
            // dhSub .4s on every step or roll (the design keys the line by both).
            float at = Since("deal-step-" + d.Step + (d.LastDie + 1) + (d.Fake ? "f" : ""));
            motions.Add(t => { float k = MenuView.CssBezier(.25f, .1f, .25f, 1, Mathf.Clamp01((t - at) / .4f)); step.style.opacity = k; step.style.translate = new Translate(0, 5 * (1 - k)); });
            card.Add(words);

            card.Add(Divider());
            var diceColumn = new VisualElement { pickingMode = PickingMode.Ignore }; diceColumn.style.alignItems = Align.Center; diceColumn.style.justifyContent = Justify.Center;
            var dice = Row(7);
            for (int i = 0; i < 2; i++) dice.Add(DieIcon(i <= d.LastDie ? (i == 0 ? d.DieA : d.DieB) : 0, i == d.LastDie));
            diceColumn.Add(dice); diceColumn.Add(Caption(L("ZAR", "DICE"), "var(--mt)")); card.Add(diceColumn);

            if (d.Revealed && d.Indicator != null)
            {
                card.Add(Divider());
                var pair = Row(10); pair.name = "deal-okey"; pair.style.alignItems = Align.FlexEnd;
                float revealed = Since("deal-indicator");
                pair.Add(DealTile(d.Indicator, false, L("GÖSTERGE", "INDICATOR"), revealed));
                // The gold arrow: M3 20h11 M10 15l5 5-5 5, stroke 2.4, round.
                var arrow = new VisualElement { pickingMode = PickingMode.Ignore }; arrow.style.width = 18; arrow.style.height = 40; arrow.style.marginBottom = 14; arrow.style.flexShrink = 0;
                arrow.generateVisualContent += c => { var p = c.painter2D; p.strokeColor = view.C("#FFC94A"); p.lineWidth = 2.4f; p.lineCap = LineCap.Round; p.lineJoin = LineJoin.Round; p.BeginPath(); p.MoveTo(new Vector2(3, 20)); p.LineTo(new Vector2(14, 20)); p.MoveTo(new Vector2(10, 15)); p.LineTo(new Vector2(15, 20)); p.LineTo(new Vector2(10, 25)); p.Stroke(); };
                pair.Add(arrow);
                pair.Add(DealTile(d.Okey, true, L("OKEY", "OKEY"), revealed + .25f));
                card.Add(pair);
            }
            row.Add(card); hudLayer.Add(row);

            if (d.Roll >= 0) BuildRollButton(d.Roll);
        }

        /// <summary>
        /// "Zar at" / "Tekrar at": padding 12 16 12 12, radius 20, gold with a 6 px ledge, 800 21px; a 40 px cream die (five
        /// pips, the centre one red) that wiggles (rollIcon), the "n/2" pill in rgba(0,0,0,.14); dhIn .4s, then rollBob.
        /// </summary>
        private void BuildRollButton(int roll)
        {
            var corner = new VisualElement { pickingMode = PickingMode.Ignore }; corner.style.position = Position.Absolute; corner.style.right = 64; corner.style.bottom = 250;
            var button = Row(12); button.name = "deal-roll"; button.focusable = true; Pad(button, 12, 16, 12, 12);
            view.Decorate(button, "linear-gradient(var(--a1),var(--a2))", 20, 6, "var(--aS)"); GalaxyFilters.Shadow(button, 0, 20, 22, new Color(0, 0, 0, .45f)); GalaxyFilters.HoverBrightness(button, 1.08f);
            var icon = new VisualElement { pickingMode = PickingMode.Ignore }; icon.style.width = icon.style.height = 40; icon.style.flexShrink = 0;
            view.Decorate(icon, "linear-gradient(145deg,#FFFDF7,#E7DDC6)", 12);
            var shade = new VisualElement { pickingMode = PickingMode.Ignore }; MenuView.Place(shade, 0, 0, 40, 40);
            shade.style.borderTopLeftRadius = shade.style.borderTopRightRadius = shade.style.borderBottomLeftRadius = shade.style.borderBottomRightRadius = 12;
            shade.style.borderBottomWidth = 3; shade.style.borderBottomColor = new Color(0, 0, 0, .12f); icon.Add(shade); // inset 0 -3px 0 rgba(0,0,0,.12)
            var pips = new VisualElement { pickingMode = PickingMode.Ignore }; MenuView.Place(pips, 7, 7, 26, 26);
            Color navy = view.C("#141733"), red = view.C("#C81E33");
            pips.generateVisualContent += c =>
            {
                var p = c.painter2D;
                foreach (var (x, y, centre) in new[] { (7f, 7f, false), (19f, 7f, false), (13f, 13f, true), (7f, 19f, false), (19f, 19f, false) })
                { p.fillColor = centre ? red : navy; p.BeginPath(); p.Arc(new Vector2(x, y), 2.6f, 0, 360); p.Fill(); }
            };
            icon.Add(pips); button.Add(icon);
            button.Add(Text(roll == 0 ? L("Zar at", "Roll") : L("Tekrar at", "Roll again"), "Montserrat-800", 21, "var(--aT)"));
            var count = Text((roll + 1) + "/2", "Baloo2-800", 12, "var(--aT)"); count.style.height = 26; count.style.unityTextAlign = TextAnchor.MiddleCenter; Pad(count, 0, 9, 0, 9);
            view.Decorate(count, "rgba(0,0,0,.14)", 13); button.Add(count);
            float began = Since("deal-roll");
            motions.Add(t =>
            {
                float e = t - began, k = MenuView.CssBezier(.22f, 1, .36f, 1, Mathf.Clamp01(e / .4f));
                // rollBob 2.2 s infinite from 1 s: 0 → −4 px → 0, ease-in-out.
                float bob = e < 1 ? 0 : -4 * MenuView.CssBezier(.42f, 0, .58f, 1, Mathf.PingPong(Mathf.Repeat(e - 1, 2.2f) / 1.1f, 1));
                corner.style.opacity = k; corner.style.translate = new Translate(0, -14 * (1 - k) + bob); corner.style.scale = new Scale(Vector2.one * (.96f + .04f * k));
                // rollIcon 2.2 s: still to 70 %, −16° at 80 %, 12° at 90 %, back at 100 %.
                float w = Mathf.Repeat(e, 2.2f) / 2.2f;
                icon.style.rotate = new Rotate(w < .7f ? 0 : w < .8f ? Mathf.Lerp(0, -16, (w - .7f) / .1f) : w < .9f ? Mathf.Lerp(-16, 12, (w - .8f) / .1f) : Mathf.Lerp(12, 0, (w - .9f) / .1f));
            });
            Clickable(button, "roll"); corner.Add(button); hudLayer.Add(corner);
        }

        private static VisualElement Divider()
        {
            var line = new VisualElement { pickingMode = PickingMode.Ignore }; line.style.width = 1; line.style.alignSelf = Align.Stretch; line.style.marginTop = line.style.marginBottom = 4;
            line.style.backgroundColor = new Color(1, 1, 1, .1f); line.style.flexShrink = 0; return line;
        }

        /// <summary>The card's captions: 900 9px, letter-spacing 1.6px, 5 px under their piece.</summary>
        private Label Caption(string text, string color)
        {
            var label = Text(text, "Baloo2-800", 9, color); MenuView.Tracking(label, 1.6f, 9); label.style.marginTop = 5; return label;
        }

        /// <summary>
        /// The card's 30 px die: padding 4, radius 8, 145° cream gradient, a 3 × 3 pip grid (gap 1, pips inset 1 px; the one
        /// pip of a 1 red). Not rolled yet: blank at 32 %. The last rolled: ring 0 0 0 2px --a1 and an 18 px gold glow; the
        /// others a 3 px shade below.
        /// </summary>
        private VisualElement DieIcon(int value, bool last)
        {
            var die = new VisualElement { pickingMode = PickingMode.Ignore }; die.style.width = die.style.height = 30; die.style.flexShrink = 0;
            view.Decorate(die, "linear-gradient(145deg,#FFFDF7,#E7DDC6)", 8, value > 0 && !last ? 3 : 0, "rgba(0,0,0,.35)");
            if (value <= 0) { die.style.opacity = .32f; return die; }
            if (last)
            {
                var ring = GalaxyFilters.SpreadRing(die, 2); ring(2, view.C("var(--a1)"));
                GalaxyFilters.Shadow(die, 0, 0, 9, new Color(1, 201 / 255f, 74 / 255f, .55f));
            }
            int[][] faces = { new[] { 4 }, new[] { 0, 8 }, new[] { 0, 4, 8 }, new[] { 0, 2, 6, 8 }, new[] { 0, 2, 4, 6, 8 }, new[] { 0, 2, 3, 5, 6, 8 } };
            var on = faces[Mathf.Clamp(value, 1, 6) - 1]; Color ink = view.C(value == 1 ? "#C81E33" : "#141733");
            const float cell = (22 - 2) / 3f;
            var dots = new VisualElement { pickingMode = PickingMode.Ignore }; MenuView.Place(dots, 0, 0, 30, 30);
            dots.generateVisualContent += c =>
            {
                var p = c.painter2D; p.fillColor = ink;
                foreach (int i in on) { p.BeginPath(); p.Arc(new Vector2(4 + (i % 3) * (cell + 1) + cell / 2, 4 + (i / 3) * (cell + 1) + cell / 2), cell / 2 - 1, 0, 360); p.Fill(); }
            };
            die.Add(dots);
            return die;
        }

        /// <summary>
        /// Indicator / okey tile: 30 × 40, radius 7, cream gradient on a 3 px #CDBE9C inset base, 0 4px 10px shadow; the
        /// number 900 18px Montserrat in the tile colour. The okey adds a 2 px #FFC94A ring and gold glow. dhTile .6 s: turns
        /// in from edge-on (90° → −8° → 0) growing .7 → 1.06 → 1.
        /// </summary>
        private VisualElement DealTile(string code, bool okey, string caption, float shownAt)
        {
            var column = new VisualElement { pickingMode = PickingMode.Ignore }; column.style.alignItems = Align.Center;
            var tile = new VisualElement { pickingMode = PickingMode.Ignore }; tile.style.width = 30; tile.style.height = 40; tile.style.flexShrink = 0; tile.style.alignItems = Align.Center; tile.style.justifyContent = Justify.Center;
            view.Decorate(tile, "linear-gradient(#FFFDF6,#EDE3CC)", 7); GalaxyFilters.Shadow(tile, 0, 4, 5, new Color(0, 0, 0, .4f));
            var inset = new VisualElement { pickingMode = PickingMode.Ignore }; MenuView.Place(inset, 0, 0, 30, 40);
            inset.style.borderTopLeftRadius = inset.style.borderTopRightRadius = inset.style.borderBottomLeftRadius = inset.style.borderBottomRightRadius = 7;
            inset.style.borderBottomWidth = 3; inset.style.borderBottomColor = view.C("#CDBE9C"); tile.Add(inset);
            if (okey) { var ring = GalaxyFilters.SpreadRing(tile, 2); ring(2, view.C("#FFC94A")); GalaxyFilters.Shadow(tile, 0, 0, 9, new Color(1, 201 / 255f, 74 / 255f, .6f)); }
            if (!string.IsNullOrEmpty(code) && code != "joker") tile.Add(Text(code.Substring(1), "Montserrat-800", 18, TileInk(code)));
            column.Add(tile); column.Add(Caption(caption, okey ? "var(--a1)" : "var(--mt)"));
            motions.Add(t =>
            {
                float u = Mathf.Clamp01((t - shownAt) / .6f);
                float angle = u < .6f ? Mathf.Lerp(90, -8, MenuView.CssBezier(.22f, 1, .36f, 1, u / .6f)) : Mathf.Lerp(-8, 0, (u - .6f) / .4f);
                float grow = u < .6f ? Mathf.Lerp(.7f, 1.06f, u / .6f) : Mathf.Lerp(1.06f, 1, (u - .6f) / .4f);
                tile.style.opacity = Mathf.Clamp01(u / .6f); tile.style.scale = new Scale(new Vector2(grow * Mathf.Max(.02f, Mathf.Abs(Mathf.Cos(angle * Mathf.Deg2Rad))), grow));
            });
            return column;
        }

        /// <summary>The design's TCOL tile inks.</summary>
        private static string TileInk(string code) => code.StartsWith("R") ? "#D7263D" : code.StartsWith("B") ? "#1F5FBF" : code.StartsWith("Y") ? "#E39B12" : "#1B1B1B";

        /// <summary>Nuclear impact: a radial white → orange flash over the whole view for 1.3 s.</summary>
        public void NuclearFlash()
        {
            if (canvas == null) return;
            var flash = new VisualElement { name = "nuclear-flash", pickingMode = PickingMode.Ignore }; MenuView.Place(flash, 0, 0, Width, Height);
            view.Decorate(flash, "radial-gradient(circle at 50% 45%,rgba(255,255,255,.95),rgba(255,180,90,.75) 40%,rgba(255,110,40,0) 80%)", 0);
            canvas.Add(flash);
            float began = Time.realtimeSinceStartup;
            flash.schedule.Execute(() => { float u = (Time.realtimeSinceStartup - began) / 1.3f; flash.style.opacity = Mathf.Clamp01(1 - u * u); if (u >= 1) flash.RemoveFromHierarchy(); }).Every(16).Until(() => flash.parent == null);
        }

    }
}
