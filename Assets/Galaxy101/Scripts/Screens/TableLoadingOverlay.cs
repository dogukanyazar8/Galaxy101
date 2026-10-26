using System;
using Galaxy101.Table;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    /// <summary>
    /// Menu → table loading screen (spec/17 §9, T50) and its failure card (T51). Drawn over the menu canvas while the table
    /// scene loads: the prototype's CSS gradient, seven stars and a large planet on the right,
    /// "BOTLARA KARŞI · 101 · 5 EL", the logo, a progress bar with its percentage and a random tip.
    /// </summary>
    public sealed class TableLoadingOverlay
    {
        private readonly MenuView view;
        private VisualElement layer, fill, errorLayer;
        private VisualElement planet, ring;
        private Label percent;

        public TableLoadingOverlay(MenuView view, Transform owner) { this.view = view; }

        public bool Visible => layer != null;
        public bool ErrorVisible => errorLayer != null;
        public VisualElement Layer => layer;

        private string Lang => view.Loc.Language;

        /// <param name="kick">The line above the logo (prototype g.kick: "KLASİK · TEKLİ · 101 · 5 EL", "DERECELİ · SOLO · …"); null = against bots.</param>
        public void Show(int hands, string kick = null)
        {
            Hide();
            var canvas = view.Root.Q<VisualElement>("galaxy-canvas") ?? view.Root.Q<VisualElement>("table-canvas") ?? view.Root;
            layer = Fill("load.screen"); layer.pickingMode = PickingMode.Position; canvas.Add(layer);
            var backdrop = Fill("table-loading-background"); backdrop.pickingMode = PickingMode.Ignore; layer.Add(backdrop);
            BuildBackdrop(backdrop);

            var column = new VisualElement { name = "table-loading-content", pickingMode = PickingMode.Ignore };
            column.style.position = Position.Absolute; column.style.left = 110; column.style.top = 190; column.style.width = 540;
            var kicker = Line(kick ?? TableStrings.Get("ldKick", Lang, hands), 12, "Baloo2-800", "var(--a1)"); kicker.name = "load.kick"; MenuView.Tracking(kicker, 2, 12);
            column.Add(kicker);
            var logo = Line("GALAXY 101", 76, "Montserrat-800", "var(--a1)", 76); logo.name = "load.logo"; MenuView.Tracking(logo, 5, 76);
            logo.style.marginTop = 16;
            column.Add(logo);
            var labels = view.Row(); labels.style.marginTop = 42; labels.style.justifyContent = Justify.SpaceBetween;
            message = Line(Message(0), 14, "Baloo2-800", "var(--ic)"); message.name = "load.msg"; labels.Add(message);
            percent = Line("0%", 14, "Baloo2-800", "var(--a1)"); percent.name = "load.pct"; labels.Add(percent);
            column.Add(labels);
            var track = new VisualElement { pickingMode = PickingMode.Ignore }; track.style.height = 12; track.style.marginTop = 8; track.style.overflow = Overflow.Hidden;
            view.Decorate(track, "rgba(255,255,255,.14)", 6);
            fill = new VisualElement { name = "load.bar", pickingMode = PickingMode.Ignore }; fill.style.position = Position.Absolute; fill.style.left = 0; fill.style.top = 0; fill.style.bottom = 0; fill.style.width = 0;
            view.Decorate(fill, "linear-gradient(90deg,var(--a2),var(--a1))", 6); track.Add(fill); column.Add(track);

            var tip = view.Row(); tip.name = "table-loading-tip"; tip.style.marginTop = 46; tip.style.paddingTop = tip.style.paddingBottom = 16; tip.style.paddingLeft = tip.style.paddingRight = 18;
            view.Decorate(tip, "var(--pn)", 22, 7, "var(--pnS)");
            var bulb = new VisualElement { pickingMode = PickingMode.Ignore }; bulb.style.width = bulb.style.height = 40; bulb.style.alignItems = Align.Center; bulb.style.justifyContent = Justify.Center; bulb.style.marginRight = 14;
            bulb.style.flexShrink = 0; view.Decorate(bulb, "linear-gradient(160deg,var(--a1),var(--a2))", 13); bulb.Add(view.Icon("table-tip", 20, "var(--aT)")); tip.Add(bulb);
            var tipText = view.Text(RandomTip(), 16, false, "#F4F1E8"); tipText.name = "load.tip"; tipText.style.unityFont = view.Font("Baloo2-700"); tipText.style.flexShrink = 1; MenuView.CssLines(tipText, 16 * 1.35f);
            tip.Add(tipText); column.Add(tip);
            layer.Add(column);
        }

        private Label message;

        // Galaxy 101 - Gerçek Ana Oyun.dc.html load.screen: this screen uses CSS artwork, not the table's 3D planet.
        private void BuildBackdrop(VisualElement backdrop)
        {
            view.Decorate(backdrop, "radial-gradient(ellipse 120% 110% at 75% 30%,var(--bg1),var(--bg2) 50%,var(--bg3))");
            foreach (var star in new[] { (12f, 18f, 1.5f, .8f), (34f, 72f, 1.5f, .6f), (58f, 12f, 2f, .7f), (88f, 84f, 1.5f, .6f), (46f, 46f, 1.5f, .5f), (22f, 90f, 2f, .6f), (94f, 22f, 1.5f, .7f) })
            {
                var point = new VisualElement { pickingMode = PickingMode.Ignore };
                MenuView.Place(point, 1280 * star.Item1 / 100 - star.Item3, 720 * star.Item2 / 100 - star.Item3, star.Item3 * 2, star.Item3 * 2);
                view.Decorate(point, "radial-gradient(circle,white,transparent)", 3); point.style.opacity = star.Item4; backdrop.Add(point);
            }
            planet = new VisualElement { name = "table-loading-planet", pickingMode = PickingMode.Ignore };
            MenuView.Place(planet, 740, 110, 420, 420);
            view.Decorate(planet, "radial-gradient(circle at 36% 30%,var(--p1),var(--p2) 45%,var(--p4) 85%)", 210);
            GalaxyFilters.Shadow(planet, 0, 0, 60, view.C("var(--pg)")); planet.style.opacity = .9f; backdrop.Add(planet);
            ring = new VisualElement { name = "table-loading-ring", pickingMode = PickingMode.Ignore };
            MenuView.Place(ring, 660, 280, 580, 96); view.Decorate(ring, "transparent", 290);
            ring.style.borderTopWidth = ring.style.borderRightWidth = ring.style.borderBottomWidth = ring.style.borderLeftWidth = 7;
            ring.style.borderTopColor = ring.style.borderRightColor = ring.style.borderBottomColor = ring.style.borderLeftColor = view.C("var(--rg)");
            ring.style.rotate = new Rotate(-14); ring.style.opacity = RingOpacity; backdrop.Add(ring);
        }

        private float RingOpacity => (float?)view.Model.Themes[view.Model.Planet]?["loadingRingOpacity"] ?? 0;

        private Label Line(string text, int size, string font, string color, float height = 0)
        {
            var label = view.Text(text, size, false, color); label.style.unityFont = view.Font(font);
            label.style.height = height > 0 ? height : MenuView.CssLineHeight(font, size);
            label.style.whiteSpace = WhiteSpace.NoWrap; label.style.unityTextAlign = TextAnchor.MiddleLeft; label.style.flexShrink = 0;
            return label;
        }

        /// <summary>The prototype's ld.msg by the percentage: connecting (≤ 25), loading (≤ 70), shuffling, ready (100).</summary>
        private string Message(float pct)
        {
            int i = pct >= 100 ? 3 : pct > 70 ? 2 : pct > 25 ? 1 : 0;
            string key = "app.STR.ldMsg." + i;
            return TableStrings.Has(key) ? TableStrings.Get(key, Lang) : view.L("3D masa yükleniyor…", "Loading the 3D table…");
        }

        /// <summary>A tip from the string table (ldTip.0, ldTip.1, …), different on each load.</summary>
        private string RandomTip()
        {
            int count = 0; while (TableStrings.Has("ldTip." + count)) count++;
            // The package's tips (app.STR.tips.*, strings_v4) when present; the shared clock's stream picks one (D-60).
            int v4 = 0; while (TableStrings.Has("app.STR.tips." + v4)) v4++;
            if (v4 > 0) return TableStrings.Get("app.STR.tips." + (int)(GameClock.Random() * v4), Lang);
            return TableStrings.Get("ldTip." + UnityEngine.Random.Range(0, Mathf.Max(1, count)), Lang);
        }

        public void SetProgress(float value)
        {
            if (fill == null) return;
            value = Mathf.Clamp01(value);
            fill.style.width = Length.Percent(value * 100); percent.text = Mathf.FloorToInt(value * 100) + "%";
            if (message != null) message.text = Message(value * 100);
        }

        /// <summary>"Masa açılamadı" (T51): the loading screen dims, a card offers "Lobiye dön" and "Yeniden dene".</summary>
        public void ShowError(Action lobby, Action retry)
        {
            if (layer == null || errorLayer != null) return;
            errorLayer = Fill("table-loading-error"); errorLayer.pickingMode = PickingMode.Position; errorLayer.style.alignItems = Align.Center; errorLayer.style.justifyContent = Justify.Center;
            errorLayer.style.backgroundColor = new Color(3 / 255f, 5 / 255f, 14 / 255f, .45f);
            var card = new VisualElement { name = "load.fail", pickingMode = PickingMode.Position }; card.style.width = 460; card.style.alignItems = Align.Center;
            card.style.paddingTop = card.style.paddingBottom = 26; card.style.paddingLeft = card.style.paddingRight = 26;
            view.Decorate(card, "var(--pn)", 22, 7, "var(--pnS)");
            card.style.borderTopWidth = card.style.borderRightWidth = card.style.borderBottomWidth = card.style.borderLeftWidth = 1;
            card.style.borderTopColor = card.style.borderRightColor = card.style.borderBottomColor = card.style.borderLeftColor = new Color(1, 1, 1, .1f);
            GalaxyFilters.Shadow(card, 0, 26, 30, new Color(0, 0, 0, .55f));
            var icon = new VisualElement { pickingMode = PickingMode.Ignore }; icon.style.width = icon.style.height = 64; icon.style.alignItems = Align.Center; icon.style.justifyContent = Justify.Center;
            view.Decorate(icon, "linear-gradient(#FF7A86,#E8455A)", 20, 5, "#9E1F33"); icon.Add(view.Icon("table-wifi-off", 30, "#fff")); card.Add(icon);
            var title = Line(TableStrings.Get("ldErrTitle", Lang), 24, "Montserrat-700", "#F4F1E8", 24 * 1.2f); title.name = "load.fail.title"; title.style.marginTop = 12; card.Add(title);
            var body = view.Text(TableStrings.Get("ldErrBody", Lang), 15, false, "var(--mt)"); body.name = "load.fail.body"; body.style.unityFont = view.Font("Baloo2-700"); body.style.maxWidth = Length.Percent(100);
            body.style.unityTextAlign = TextAnchor.MiddleCenter; body.style.marginTop = 12; MenuView.CssLines(body, 15 * 1.45f); card.Add(body);
            var buttons = view.Row(); buttons.style.alignSelf = Align.Stretch; buttons.style.marginTop = 18;
            var back = FailureButton("load.fail.lobby", TableStrings.Get("msLobby", Lang), () => lobby?.Invoke(), false); back.style.marginRight = 10;
            var again = FailureButton("load.fail.retry", TableStrings.Get("ldRetry", Lang), () => { HideError(); retry?.Invoke(); }, true);
            buttons.Add(back); buttons.Add(again); card.Add(buttons);
            errorLayer.Add(card); layer.Add(errorLayer);
            planet.style.opacity = .5f; ring.style.opacity = .4f;
        }

        private Button FailureButton(string id, string text, Action click, bool primary)
        {
            var button = view.Button(text, click, primary ? "primary bright-107" : "secondary bright-120"); button.name = id;
            button.style.flexGrow = 1; button.style.flexBasis = 0; button.style.unityFont = view.Font("Montserrat-800");
            button.style.height = MenuView.CssLineHeight("Montserrat-800", 15) + 26;
            button.style.paddingTop = button.style.paddingBottom = button.style.paddingLeft = button.style.paddingRight = 13;
            view.Decorate(button, primary ? "linear-gradient(var(--a1),var(--a2))" : "var(--pnH)", 15, 5, primary ? "var(--aS)" : "var(--pnS)");
            return button;
        }

        public void HideError()
        {
            errorLayer?.RemoveFromHierarchy(); errorLayer = null;
            if (planet != null) planet.style.opacity = .9f;
            if (ring != null) ring.style.opacity = RingOpacity;
        }

        public void Hide()
        {
            HideError();
            layer?.RemoveFromHierarchy(); layer = null; fill = null; percent = null;
            planet = ring = null;
        }

        private static VisualElement Fill(string name)
        {
            var element = new VisualElement { name = name };
            element.style.position = Position.Absolute; element.style.left = element.style.top = element.style.right = element.style.bottom = 0;
            return element;
        }
    }
}
