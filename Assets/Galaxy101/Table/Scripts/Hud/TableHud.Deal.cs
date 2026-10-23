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

        private static readonly string[] SuitColors = { "R", "#E5484D", "B", "#2F7CF6", "Y", "#E8A317", "K", "#1B2440" };

        private string SuitColor(string code)
        {
            for (int i = 0; i < SuitColors.Length; i += 2) if (code.StartsWith(SuitColors[i])) return SuitColors[i + 1];
            return "#1B2440";
        }

        private string DealStepText(DealInfoCard d)
        {
            string dealer = d.Dealer == 0 ? null : FirstName(d.Dealer), starter = (d.Dealer + 1) % 4 == 0 ? null : FirstName((d.Dealer + 1) % 4);
            switch (d.Step)
            {
                case "stack": return L("106 taş 7'li balyalara diziliyor", "106 tiles stacked in sevens");
                case "roll": return d.Dealer == 0 ? L("Zarı iki kez at", "Roll the die twice") : L(dealer + " zar atıyor", dealer + " is rolling");
                case "rolling": return L("Zar yuvarlanıyor…", "The die is rolling…");
                case "indicator": return d.Fake ? L("Sahte okey çıktı · üstündeki taş gösterge oldu", "A fake okey came up · the tile above is the indicator") : L(d.DieA + ". balya · alttan " + d.DieB + ". taş gösterge", "Stack " + d.DieA + " · tile " + d.DieB + " from the bottom is the indicator");
                case "extra": return L("Fazla taş yandaki balyanın üstüne", "The extra tile goes onto the next stack");
                case "deal": return L("Balyalar dağıtılıyor", "Dealing the stacks");
                case "centre": return L("Kalan balyalar ortaya", "The remaining stacks go to the centre");
                default: return starter == null ? L("22 taşla sen başlıyorsun", "You start with 22 tiles") : L("22 taşla " + starter + " başlıyor", starter + " starts with 22 tiles");
            }
        }

        /// <summary>Glass card at top 84: dealer avatar + DAĞITAN, title, the step (slides in on change), dice, indicator → okey.</summary>
        private void BuildDeal(DealInfoCard d)
        {
            var row = FullRow(84);
            var card = Row(14); card.name = "deal-card"; Pad(card, 12, 20, 12, 14);
            view.Decorate(card, "rgba(5,8,22,.62)", 22); Border(card, 1, new Color(1, 1, 1, .1f)); GalaxyFilters.BackdropBlur(card, 8);
            var who = new VisualElement { pickingMode = PickingMode.Ignore }; who.style.alignItems = Align.Center;
            who.Add(Avatar(Seat(d.Dealer), 44, false, 0, false));
            var badge = Text(L("DAĞITAN", "DEALER"), "Baloo2-800", 9, "var(--aT)"); MenuView.Tracking(badge, 1.2f, 9); badge.style.marginTop = -6; Pad(badge, 1, 6, 1, 6); view.Decorate(badge, "var(--a1)", 6);
            who.Add(badge); card.Add(who);
            var words = new VisualElement { pickingMode = PickingMode.Ignore }; words.style.minWidth = 300;
            words.Add(Text(d.Dealer == 0 ? L("Taşları sen dağıtıyorsun", "You are dealing") : L("Taşları " + FirstName(d.Dealer) + " dağıtıyor", FirstName(d.Dealer) + " is dealing"), "Montserrat-700", 17, "#F4F1E8"));
            var step = Text(DealStepText(d), "Baloo2-700", 13, "var(--ic)"); step.name = "deal-step"; step.style.marginTop = 3; words.Add(step);
            float at = d.StepAt;
            motions.Add(t => { float u = Mathf.Clamp01((Time.realtimeSinceStartup - at) / .4f), k = TableConstants.EaseOutCubic(u); step.style.opacity = k; step.style.translate = new Translate(10 * (1 - k), 0); });
            card.Add(words);
            var dice = Row(8);
            for (int i = 0; i < 2; i++) dice.Add(DieIcon(i == 0 ? d.DieA : d.DieB, i <= d.LastDie, i == d.LastDie));
            card.Add(dice);
            if (d.Revealed && d.Indicator != null)
            {
                var pair = Row(6); pair.name = "deal-okey";
                pair.Add(MiniTile(d.Indicator, false));
                pair.Add(Text("→", "Montserrat-800", 16, "var(--mt)"));
                pair.Add(MiniTile(d.Okey, true));
                card.Add(pair);
            }
            row.Add(card); hudLayer.Add(row);

            if (d.Roll >= 0)
            {
                // G04: right of the felt, beside the seat on my right.
                var bottom = new VisualElement { pickingMode = PickingMode.Ignore }; bottom.style.position = Position.Absolute; bottom.style.right = 56; bottom.style.top = 404;
                var button = Row(10); button.name = "deal-roll"; button.focusable = true; Pad(button, 14, 30, 14, 26);
                view.Decorate(button, "linear-gradient(var(--a1),var(--a2))", 20, 6, "var(--aS)"); GalaxyFilters.HoverBrightness(button, 1.07f);
                button.Add(DieIcon(5, true, false));
                button.Add(Text(d.Roll == 0 ? L("Zar at", "Roll") : L("Tekrar at", "Roll again"), "Montserrat-800", 22, "var(--aT)"));
                var count = Text((d.Roll + 1) + "/2", "Baloo2-800", 12, "var(--a1)"); Pad(count, 2, 8, 2, 8); view.Decorate(count, "var(--aT)", 8); button.Add(count);
                float shown = Time.realtimeSinceStartup;
                motions.Add(t => { float p = Mathf.Repeat((Time.realtimeSinceStartup - shown) / 1.6f, 1); button.style.scale = new Scale(Vector2.one * (1 + .035f * Mathf.Sin(p * Mathf.PI * 2))); });
                Clickable(button, "roll"); bottom.Add(button); hudLayer.Add(bottom);
            }
        }

        /// <summary>30 px die: 3×3 pip grid; not rolled yet at 32 %; the last rolled one with a gold ring.</summary>
        private VisualElement DieIcon(int value, bool rolled, bool last)
        {
            var die = new VisualElement { pickingMode = PickingMode.Ignore }; die.style.width = die.style.height = 30; die.style.flexShrink = 0;
            if (value == 0) { view.Decorate(die, "rgba(255,255,255,.32)", 8); return die; }
            view.Decorate(die, "#F6F0E0", 8); die.style.opacity = rolled ? 1 : .32f;
            if (last) Border(die, 2, view.C("var(--a1)"));
            bool[] on = value switch
            {
                1 => new[] { false, false, false, false, true, false, false, false, false },
                2 => new[] { true, false, false, false, false, false, false, false, true },
                3 => new[] { true, false, false, false, true, false, false, false, true },
                4 => new[] { true, false, true, false, false, false, true, false, true },
                5 => new[] { true, false, true, false, true, false, true, false, true },
                6 => new[] { true, false, true, true, false, true, true, false, true },
                _ => new[] { true, false, true, false, true, false, true, false, true },
            };
            if (!rolled && value != 0) on = new[] { false, false, false, false, false, false, false, false, false };
            for (int i = 0; i < 9; i++)
            {
                if (!on[i]) continue;
                var pip = new VisualElement { pickingMode = PickingMode.Ignore }; int size = value == 1 ? 8 : 5;
                MenuView.Place(pip, 4 + (i % 3) * 8.5f + (5 - size) / 2f, 4 + (i / 3) * 8.5f + (5 - size) / 2f, size, size);
                view.Decorate(pip, value == 1 ? "#D9343F" : "#1B2440", size); die.Add(pip);
            }
            if (!rolled && value != 0) { var q = Text("?", "Montserrat-800", 15, "#1B2440"); q.style.unityTextAlign = TextAnchor.MiddleCenter; MenuView.Place(q, 0, 0, 30, 30); die.Add(q); }
            return die;
        }

        private VisualElement MiniTile(string code, bool okey)
        {
            var tile = new VisualElement { pickingMode = PickingMode.Ignore }; tile.style.width = 26; tile.style.height = 36; tile.style.flexShrink = 0;
            tile.style.alignItems = Align.Center; tile.style.justifyContent = Justify.Center;
            view.Decorate(tile, "linear-gradient(var(--tf1),var(--tf2))", 6, 3, "var(--te2)");
            if (okey) { Border(tile, 2, view.C("var(--a1)")); GalaxyFilters.Shadow(tile, 0, 0, 6, view.C("var(--gl)")); }
            if (!string.IsNullOrEmpty(code) && code != "joker") tile.Add(Text(code.Substring(1), "Montserrat-800", 15, SuitColor(code)));
            return tile;
        }
    }
}
