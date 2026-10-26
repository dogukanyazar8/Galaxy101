using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101.Table
{
    public sealed partial class TableHud
    {
        /// <summary>
        /// T26 (G101-Masa netOpen, protocol.json reconnect): over a dimmed, blurred table a 470 px card — the Wi-Fi arcs,
        /// "Bağlantı koptu", the bot-plays-for-you line, "Deneme n/5" with the spinner and "Bağlanıyor", the 3 s attempt bar,
        /// then "Lobiye dön" and "Şimdi dene". After the fifth attempt only "Lobiye dön" acts.
        /// </summary>
        private void BuildNet(State s)
        {
            var layer = new VisualElement { name = "table-net", pickingMode = PickingMode.Position };
            layer.style.position = Position.Absolute; layer.style.left = layer.style.top = layer.style.right = layer.style.bottom = 0;
            layer.style.alignItems = Align.Center; layer.style.justifyContent = Justify.Center;
            var dim = new VisualElement { pickingMode = PickingMode.Ignore }; dim.style.position = Position.Absolute; dim.style.left = dim.style.top = dim.style.right = dim.style.bottom = 0;
            dim.style.backgroundColor = new Color(2 / 255f, 4 / 255f, 14 / 255f, .66f); layer.Add(dim);
            var card = new VisualElement { name = "table-net-card", pickingMode = PickingMode.Position };
            card.style.width = 470; card.style.alignItems = Align.Center; Pad(card, 30, 28, 26, 28);
            view.Decorate(card, "var(--pn)", 28, 10, "var(--pnS)"); Border(card, 1, new Color(1, 1, 1, .1f)); GalaxyFilters.Shadow(card, 0, 40, 90, new Color(0, 0, 0, .6f));
            float shown = Since("table-net");
            motions.Add(t => { float k = MenuView.CssBezier(.2f, 1.2f, .4f, 1, Mathf.Clamp01((t - shown) / .28f)); card.style.opacity = Mathf.Clamp01(k * 2); card.style.scale = new Scale(Vector2.one * (.9f + .1f * k)); });

            // Three arcs and a dot, each arc pulsing (wifi 1.6 s, 0 / .3 / .6 s apart).
            var icon = new VisualElement { pickingMode = PickingMode.Ignore }; icon.style.width = 76; icon.style.height = 62;
            for (int i = 0; i < 3; i++)
            {
                float size = 64 - i * 20, delay = .6f - i * .3f;
                var arc = new ArcRing(view.C("var(--a1)"), new Color(0, 0, 0, 0)) { Fraction = .25f };
                MenuView.Place(arc, 38 - size / 2, 6 + i * 11, size, size); arc.style.rotate = new Rotate(-45);
                motions.Add(t => arc.style.opacity = .35f + .65f * Mathf.Abs(Mathf.Sin(Mathf.PI * Mathf.Repeat(t - delay, 1.6f) / 1.6f)));
                icon.Add(arc);
            }
            var dot = new VisualElement { pickingMode = PickingMode.Ignore }; MenuView.Place(dot, 34, 50, 8, 8); view.Decorate(dot, "var(--a1)", 4); icon.Add(dot);
            card.Add(icon);

            var title = Text(TableStrings.Get(s.NetReplaced ? "netReplacedTitle" : "netTitle", Lang), "Montserrat-700", 26, "#F4F1E8"); title.name = "table.t.netTitle"; title.style.marginTop = 12; card.Add(title);
            var body = Text(TableStrings.Get(s.NetReplaced ? "netReplacedBody" : "netBody", Lang), "Baloo2-700", 15, "var(--mt)"); body.name = "table.t.netBody";
            body.style.whiteSpace = WhiteSpace.Normal; body.style.maxWidth = 380; body.style.unityTextAlign = TextAnchor.UpperCenter; body.style.marginTop = 12; card.Add(body);

            if (s.NetReplaced)
            {
                // No attempts to count down: only the way back to the lobby.
                var back = Text(TableStrings.Get("lobby", Lang), "Montserrat-700", 16, "var(--aT)"); back.name = "table.askExitNet"; back.style.alignSelf = Align.Stretch; back.style.marginTop = 20;
                back.style.unityTextAlign = TextAnchor.MiddleCenter; Pad(back, 14, 14, 14, 14); view.Decorate(back, "linear-gradient(var(--a1),var(--a2))", 16, 5, "var(--aS)");
                card.Add(Clickable(back, "net-lobby"));
                layer.Add(card);
                hudLayer.Add(layer);
                return;
            }

            var progress = new VisualElement { pickingMode = PickingMode.Ignore }; progress.style.alignSelf = Align.Stretch; progress.style.marginTop = 18;
            var line = Row(6); line.style.justifyContent = Justify.SpaceBetween;
            var attempt = Text(TableStrings.Get("netAttempt", Lang, Mathf.Clamp(s.NetAttempt, 1, 5)), "Baloo2-800", 12, "var(--mt)"); attempt.name = "table.netAttempt"; line.Add(attempt);
            var waiting = Row(6);
            var spinner = new ArcRing(view.C("var(--a1)"), new Color(0, 0, 0, 0)) { Fraction = .75f }; spinner.style.width = spinner.style.height = 12;
            motions.Add(t => spinner.style.rotate = new Rotate(Mathf.Repeat(t, .8f) / .8f * 360)); waiting.Add(spinner);
            waiting.Add(Text(TableStrings.Get("netWait", Lang), "Baloo2-800", 12, "var(--mt)")); line.Add(waiting);
            progress.Add(line);
            var bar = new VisualElement { name = "table.netBarEl", pickingMode = PickingMode.Ignore }; bar.style.height = 10; bar.style.marginTop = 6; bar.style.overflow = Overflow.Hidden; view.Decorate(bar, "var(--dp)", 5);
            var fill = new VisualElement { pickingMode = PickingMode.Ignore }; fill.style.position = Position.Absolute; fill.style.left = fill.style.top = fill.style.bottom = 0;
            view.Decorate(fill, "linear-gradient(90deg,var(--a2),var(--a1))", 5); bar.Add(fill);
            float attemptStarted = Since("net-attempt-" + s.NetAttempt);
            motions.Add(t => fill.style.width = Length.Percent(s.NetGaveUp ? 100 : Mathf.Clamp01((t - attemptStarted) / (TableTimings.ClientRetryMs / 1000f)) * 100));
            progress.Add(bar); card.Add(progress);

            var buttons = Row(10); buttons.style.alignSelf = Align.Stretch; buttons.style.marginTop = 20;
            var lobby = Text(TableStrings.Get("lobby", Lang), "Montserrat-700", 16, "#F4F1E8"); lobby.name = "table.askExitNet"; lobby.style.flexGrow = 1; lobby.style.flexBasis = 0; lobby.style.unityTextAlign = TextAnchor.MiddleCenter;
            Pad(lobby, 14, 14, 14, 14); view.Decorate(lobby, "var(--pnH)", 16, 5, "var(--pnS)"); buttons.Add(Clickable(lobby, "net-lobby"));
            var retry = Text(TableStrings.Get("retry", Lang), "Montserrat-700", 16, "var(--aT)"); retry.name = "table.retryNet"; retry.style.flexGrow = 1; retry.style.flexBasis = 0; retry.style.unityTextAlign = TextAnchor.MiddleCenter;
            Pad(retry, 14, 14, 14, 14); view.Decorate(retry, "linear-gradient(var(--a1),var(--a2))", 16, 5, "var(--aS)"); retry.style.opacity = s.NetGaveUp ? .5f : 1;
            buttons.Add(s.NetGaveUp ? retry : Clickable(retry, "net-retry"));
            card.Add(buttons);
            layer.Add(card);
            hudLayer.Add(layer);
        }
    }
}
