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
            dim.style.backgroundColor = new Color(2 / 255f, 4 / 255f, 14 / 255f, .66f); GalaxyFilters.BackdropBlur(dim, 8); layer.Add(dim);   // blur(8px) grayscale(.6)
            var card = new VisualElement { name = "table-net-card", pickingMode = PickingMode.Position };
            // width: 470px with padding 30px 28px 26px in a content-box: the card is 526 px wide.
            card.style.width = 470 + 2 * 28; card.style.alignItems = Align.Center; Pad(card, 30, 28, 26, 28);
            view.Decorate(card, "var(--pn)", 28, 10, "var(--pnS)"); Border(card, 1, new Color(1, 1, 1, .1f)); GalaxyFilters.Shadow(card, 0, 40, 90, new Color(0, 0, 0, .6f));
            float shown = Since("table-net");
            motions.Add(t => { float k = MenuView.CssBezier(.2f, 1.2f, .4f, 1, Mathf.Clamp01((t - shown) / .28f)); card.style.opacity = Mathf.Clamp01(k * 2); card.style.scale = new Scale(Vector2.one * (.9f + .1f * k)); });

            // The 76×62 svg: three stroked arcs (width 6, round caps: M6 22a46 46 0 0 1 64 0, M16 33a32…, M27 44a17…) and a
            // 4 px dot at (38, 54); each arc pulses (wifi 1.6 s: opacity .2 → 1 → .2, delays .6 / .3 / 0 s).
            var icon = new VisualElement { pickingMode = PickingMode.Ignore }; icon.style.width = 76; icon.style.height = 62;
            var arcs = new (float X0, float X1, float Y, float R, float Delay)[] { (6, 70, 22, 46, .6f), (16, 60, 33, 32, .3f), (27, 49, 44, 17, 0) };
            var arcOpacity = new float[3]; var gold = view.C("var(--a1)");
            icon.generateVisualContent += context =>
            {
                var p = context.painter2D; p.lineWidth = 6; p.lineCap = LineCap.Round;
                for (int i = 0; i < 3; i++)
                {
                    var (x0, x1, y, r, _) = arcs[i]; float half = (x1 - x0) / 2, cy = y + Mathf.Sqrt(r * r - half * half);
                    float a0 = Mathf.Atan2(y - cy, x0 - (x0 + half)) * Mathf.Rad2Deg, a1 = Mathf.Atan2(y - cy, x1 - (x0 + half)) * Mathf.Rad2Deg;
                    var c = gold; c.a *= arcOpacity[i]; p.strokeColor = c;
                    p.BeginPath(); p.Arc(new Vector2(x0 + half, cy), r, a0, a1); p.Stroke();
                }
                p.fillColor = gold; p.BeginPath(); p.Arc(new Vector2(38, 54), 4, 0, 360); p.Fill();
            };
            motions.Add(t =>
            {
                for (int i = 0; i < 3; i++) { float u = Mathf.Repeat(t - arcs[i].Delay, 1.6f) / 1.6f; arcOpacity[i] = .2f + .8f * (1 - Mathf.Abs(1 - 2 * u)); }
                icon.MarkDirtyRepaint();
            });
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
            // barFill 3 s linear both: full at the g101-clock freeze (the animation has run out at the canonical instant).
            motions.Add(t => fill.style.width = Length.Percent(s.NetGaveUp || GameClock.Frozen ? 100 : Mathf.Clamp01((t - attemptStarted) / (TableTimings.ClientRetryMs / 1000f)) * 100));
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
