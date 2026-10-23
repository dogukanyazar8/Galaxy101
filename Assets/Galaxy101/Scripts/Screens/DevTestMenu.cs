using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    /// <summary>
    /// Geliştirici test menüsü (spec/25), editor and development builds only: F9 on the main menu lists the prototype's
    /// test scenarios and opens the 3D table straight into one (prepared hands from <see cref="Table.TableScenarios"/>).
    /// </summary>
    public sealed class DevTestMenu
    {
        public static bool Available => Application.isEditor || Debug.isDebugBuild;

        public static readonly (string Key, string Tr, string En, string Expect)[] Scenarios =
        {
            ("seat", "Bekleme", "Waiting", "1/4 → 4/4 oyuncu, 3-2-1 · sonra dağıtım töreni"),
            ("deal", "Dağıtım", "Deal", "Törenin tamamı (dağıtan rastgele)"),
            ("play", "Oyun", "Play", "Ortada el, hızlı dağıtım"),
            ("opening", "El açma", "Opening", "Hazır açılabilir el · per ortaya, deste sola"),
            ("processing", "İşleme testi", "Laying off", "Herkes açmış, 15 per · işle, okey al, bitir"),
            ("shortOpening", "Hatalı açma cezası", "Wrong opening", "Perler 66 · Aç turuncu, +101"),
            ("playable", "İşlek taş cezası", "Playable tile", "İşleme masası · işlek atan +101"),
            ("fromHand", "Elden bitiş", "Finish from hand", "Açılmamış, 21 taş perde + 1 · Elden bit"),
        };

        private readonly MenuView view;
        private readonly Action<string> open;
        private VisualElement layer;
        public bool Visible => layer != null;

        public DevTestMenu(MenuView view, Action<string> open) { this.view = view; this.open = open; }

        public void Toggle() { if (layer != null) Hide(); else Show(); }

        public void Hide() { layer?.RemoveFromHierarchy(); layer = null; }

        public void Show()
        {
            if (!Available || layer != null) return;
            var canvas = view.Root.Q<VisualElement>("galaxy-canvas") ?? view.Root;
            layer = new VisualElement { name = "dev-test-menu" }; MenuView.Place(layer, 0, 0, 1280, 720); view.Decorate(layer, "rgba(0,0,0,.6)");
            layer.RegisterCallback<ClickEvent>(e => { if (e.target == layer) Hide(); });
            var panel = new VisualElement(); MenuView.Place(panel, 400, 70, 480, 580); view.Decorate(panel, "var(--pn)", 24, 8, "var(--pnS)");
            panel.style.paddingTop = panel.style.paddingBottom = 18; panel.style.paddingLeft = panel.style.paddingRight = 20;
            var title = view.Text(view.L("TEST SENARYOSU", "TEST SCENARIO"), 12, false, "var(--a1)"); title.style.unityFont = view.Font("Baloo2-800"); MenuView.Tracking(title, 1.8f, 12); panel.Add(title);
            var sub = view.Text(view.L("Geliştirici menüsü · F9", "Developer menu · F9"), 13, false, "var(--mt)"); sub.style.marginBottom = 10; panel.Add(sub);
            foreach (var (key, tr, en, expect) in Scenarios)
            {
                string pick = key;
                var row = new VisualElement { name = "dev-scenario-" + key, focusable = true }; row.style.marginTop = 6;
                row.style.paddingTop = row.style.paddingBottom = 9; row.style.paddingLeft = row.style.paddingRight = 14; view.Decorate(row, "var(--pnH)", 14, 3, "var(--pnS)");
                row.Add(view.Text(view.L(tr, en), 16, true, "#F4F1E8"));
                var note = view.Text(expect, 12, false, "var(--mt)"); note.style.whiteSpace = WhiteSpace.Normal; row.Add(note);
                GalaxyFilters.HoverBrightness(row, 1.15f);
                row.RegisterCallback<ClickEvent>(e => { e.StopPropagation(); Hide(); open(pick); });
                panel.Add(row);
            }
            layer.Add(panel); canvas.Add(layer);
        }
    }
}
