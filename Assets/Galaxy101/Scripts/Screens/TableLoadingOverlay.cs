using System;
using Galaxy101.Table;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    /// <summary>
    /// Menu → table loading screen (spec/17 §9, T50) and its failure card (T51). Drawn over the menu canvas while the table
    /// scene loads: theme gradient, stars and a large planet on the right (its own GalaxyBackground in a static layout),
    /// "BOTLARA KARŞI · 101 · 5 EL", the logo, a progress bar with its percentage and a random tip.
    /// </summary>
    public sealed class TableLoadingOverlay
    {
        private readonly MenuView view;
        private readonly Transform owner;
        private VisualElement layer, fill, errorLayer;
        private Label percent;
        private GalaxyBackground background;

        public TableLoadingOverlay(MenuView view, Transform owner) { this.view = view; this.owner = owner; }

        public bool Visible => layer != null;
        public bool ErrorVisible => errorLayer != null;
        public VisualElement Layer => layer;

        private string Lang => view.Loc.Language;

        public void Show(int hands)
        {
            Hide();
            var canvas = view.Root.Q<VisualElement>("galaxy-canvas") ?? view.Root;
            layer = Fill("table-loading"); layer.pickingMode = PickingMode.Position; canvas.Add(layer);
            var backdrop = Fill("table-loading-background"); backdrop.pickingMode = PickingMode.Ignore; layer.Add(backdrop);
            background = new GameObject("Table loading background").AddComponent<GalaxyBackground>(); background.transform.SetParent(owner, false);
            background.Initialize(backdrop);
            background.Configure(view.Model.Planet, (bool?)view.Model.Settings["motion"] ?? true, (string)view.Model.Settings["quality"] ?? "Yüksek");
            // Planet on the right (centre 966,324, ×1.4) inside a soft pg halo; the gradient brightens towards the top.
            background.UseStaticLayout(new Vector4(700, 160, 1300, 760), new Vector4(966, 324, 420, 1), new Vector4(966, 324, 1.4f, 0), view.C("var(--pg)"));
            background.SetFocused(UnityEngine.Application.isFocused || UnityEngine.Application.isBatchMode);

            var column = new VisualElement { name = "table-loading-content", pickingMode = PickingMode.Ignore };
            MenuView.Place(column, 112, 196, 548, 320);
            var kick = view.Text(TableStrings.Get("ldKick", Lang, hands), 11, false, "var(--a1)"); kick.style.unityFont = view.Font("Baloo2-800"); MenuView.Tracking(kick, 2, 11);
            column.Add(kick);
            var logo = view.Text("GALAXY 101", 76, true, "var(--a1)"); logo.style.unityFont = view.Font("Montserrat-800"); MenuView.Tracking(logo, 3, 76);
            logo.style.textShadow = new TextShadow { offset = new Vector2(0, 5), blurRadius = 0, color = view.C("var(--lS)") }; logo.style.marginTop = 2;
            column.Add(logo);
            var labels = view.Row(); labels.style.marginTop = 22; labels.style.justifyContent = Justify.SpaceBetween;
            var loading = view.Text(view.L("3D masa yükleniyor…", "Loading the 3D table…"), 12, false, "var(--ic)"); loading.style.unityFont = view.Font("Baloo2-800"); labels.Add(loading);
            percent = view.Text("0%", 12, false, "var(--a1)"); percent.name = "table-loading-percent"; percent.style.unityFont = view.Font("Baloo2-800"); labels.Add(percent);
            column.Add(labels);
            var track = new VisualElement { pickingMode = PickingMode.Ignore }; track.style.height = 12; track.style.marginTop = 8; track.style.overflow = Overflow.Hidden;
            view.Decorate(track, "rgba(255,255,255,.14)", 6);
            fill = new VisualElement { name = "table-loading-fill", pickingMode = PickingMode.Ignore }; fill.style.position = Position.Absolute; fill.style.left = 0; fill.style.top = 0; fill.style.bottom = 0; fill.style.width = 0;
            view.Decorate(fill, "linear-gradient(90deg,var(--a2),var(--a1))", 6); track.Add(fill); column.Add(track);

            var tip = view.Row(); tip.name = "table-loading-tip"; tip.style.marginTop = 40; tip.style.paddingTop = tip.style.paddingBottom = 14; tip.style.paddingLeft = tip.style.paddingRight = 16;
            view.Decorate(tip, "var(--pn)", 18, 5, "var(--pnS)");
            var bulb = new VisualElement { pickingMode = PickingMode.Ignore }; bulb.style.width = bulb.style.height = 40; bulb.style.alignItems = Align.Center; bulb.style.justifyContent = Justify.Center; bulb.style.marginRight = 14;
            view.Decorate(bulb, "linear-gradient(var(--a1),var(--a2))", 12, 3, "var(--aS)"); bulb.Add(view.Icon("table-tip", 22, "var(--aT)")); tip.Add(bulb);
            var tipText = view.Text(RandomTip(), 14, false, "#F4F1E8"); tipText.style.unityFont = view.Font("Baloo2-800"); tipText.style.whiteSpace = WhiteSpace.Normal; tipText.style.flexShrink = 1;
            tip.Add(tipText); column.Add(tip);
            layer.Add(column);
        }

        /// <summary>A tip from the string table (ldTip.0, ldTip.1, …), different on each load.</summary>
        private string RandomTip()
        {
            int count = 0; while (TableStrings.Has("ldTip." + count)) count++;
            return TableStrings.Get("ldTip." + UnityEngine.Random.Range(0, Mathf.Max(1, count)), Lang);
        }

        public void SetProgress(float value)
        {
            if (fill == null) return;
            value = Mathf.Clamp01(value);
            fill.style.width = Length.Percent(value * 100); percent.text = Mathf.RoundToInt(value * 100) + "%";
        }

        /// <summary>"Masa açılamadı" (T51): the loading screen dims, a card offers "Lobiye dön" and "Yeniden dene".</summary>
        public void ShowError(Action lobby, Action retry)
        {
            if (layer == null || errorLayer != null) return;
            errorLayer = Fill("table-loading-error"); errorLayer.pickingMode = PickingMode.Position; errorLayer.style.alignItems = Align.Center; errorLayer.style.justifyContent = Justify.Center;
            errorLayer.style.backgroundColor = new Color(.012f, .02f, .055f, .5f);
            var card = new VisualElement { name = "table-loading-error-card", pickingMode = PickingMode.Position }; card.style.width = 460; card.style.alignItems = Align.Center;
            card.style.paddingTop = card.style.paddingBottom = 24; card.style.paddingLeft = card.style.paddingRight = 24;
            view.Decorate(card, "var(--pn)", 22, 7, "var(--pnS)");
            var icon = new VisualElement { pickingMode = PickingMode.Ignore }; icon.style.width = icon.style.height = 64; icon.style.alignItems = Align.Center; icon.style.justifyContent = Justify.Center;
            view.Decorate(icon, "linear-gradient(#FF7A86,#E8455A)", 20, 5, "#9E1F33"); icon.Add(view.Icon("table-wifi-off", 30, "#fff")); card.Add(icon);
            var title = view.Text(TableStrings.Get("ldErrTitle", Lang), 24, true, "#F4F1E8"); title.style.marginTop = 12; card.Add(title);
            var body = view.Text(TableStrings.Get("ldErrBody", Lang), 14, false, "var(--mt)"); body.style.unityFont = view.Font("Baloo2-800"); body.style.whiteSpace = WhiteSpace.Normal;
            body.style.unityTextAlign = TextAnchor.UpperCenter; body.style.marginTop = 8; card.Add(body);
            var buttons = view.Row(); buttons.style.alignSelf = Align.Stretch; buttons.style.marginTop = 18;
            var back = view.Button(TableStrings.Get("msLobby", Lang), () => lobby?.Invoke(), "secondary"); back.name = "table-loading-lobby"; back.style.flexGrow = 1; back.style.flexBasis = 0; back.style.marginRight = 10;
            var again = view.Button(TableStrings.Get("ldRetry", Lang), () => { HideError(); retry?.Invoke(); }, "primary"); again.name = "table-loading-retry"; again.style.flexGrow = 1; again.style.flexBasis = 0;
            buttons.Add(back); buttons.Add(again); card.Add(buttons);
            errorLayer.Add(card); layer.Add(errorLayer);
            // T51: only the dimmed background stays behind the card.
            var content = layer.Q<VisualElement>("table-loading-content"); if (content != null) content.style.display = DisplayStyle.None;
        }

        public void HideError()
        {
            errorLayer?.RemoveFromHierarchy(); errorLayer = null;
            var content = layer?.Q<VisualElement>("table-loading-content"); if (content != null) content.style.display = DisplayStyle.Flex;
        }

        public void Hide()
        {
            HideError();
            layer?.RemoveFromHierarchy(); layer = null; fill = null; percent = null;
            if (background != null) { UnityEngine.Object.Destroy(background.gameObject); background = null; }
        }

        private static VisualElement Fill(string name)
        {
            var element = new VisualElement { name = name };
            element.style.position = Position.Absolute; element.style.left = element.style.top = element.style.right = element.style.bottom = 0;
            return element;
        }
    }
}
