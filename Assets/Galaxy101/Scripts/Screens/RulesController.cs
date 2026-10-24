using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace Galaxy101
{
    public sealed class RulesController
    {
        public VisualElement Build(MenuView v)
        {
            var root = PanelUi.Shell(v, "Rules");
            root.style.alignItems = Align.Stretch;
            var rules = (JArray)v.Model.Content["rules"];
            var selected = v.Tab("rules");
            var nav = root.Q<VisualElement>("navigation"); nav.style.width = 230; nav.style.flexShrink = 0; nav.style.marginRight = 18;
            for (var i = 0; i < rules.Count; i++)
            {
                var index = i; var entry = (JObject)rules[i];
                var text = v.L((string)entry["tr"][0], (string)entry["en"][0]);
                var button = PanelUi.CustomButton(v, "", () => v.SetTab("rules", index), "g-rule-nav");
                button.name = "rule-section-" + index;
                button.EnableInClassList("selected", selected == index);
                button.style.flexDirection = FlexDirection.Row; button.style.alignItems = Align.Center; button.style.justifyContent = Justify.FlexStart;
                // Reference: 28px number tile (radius 9, 700 14px heading font), gap 12, 700 16px/1.2 label; inset bar marks the section.
                if (index == selected) PanelUi.InsetBar(v, button, 14);
                var number = v.Box(); number.style.width = number.style.height = 28; number.style.flexShrink = 0; number.style.marginRight = 12; number.style.alignItems = Align.Center; number.style.justifyContent = Justify.Center;
                v.Decorate(number, index == selected ? "linear-gradient(var(--a1),var(--a2))" : "dp", 9);
                var digit = v.Text((index + 1).ToString(), 14, true, index == selected ? "aT" : "mt"); digit.style.unityTextAlign = UnityEngine.TextAnchor.MiddleCenter; number.Add(digit);
                var label = v.Text(text, 16, true); label.style.color = StyleKeyword.Null; label.style.height = 19.2f; label.style.unityTextAlign = UnityEngine.TextAnchor.MiddleLeft;
                button.Add(number); button.Add(label); nav.Add(button);
            }
            var data = (JObject)rules[selected];
            var language = v.Loc.Language == "en" ? "en" : "tr";
            var content = v.Scroll(); content.AddToClassList("g-panel-card"); PanelUi.Pad(content, 24, 28);
            root.Q<VisualElement>("content").Add(content);
            var title = v.Text((string)data[language][0], 28, true); title.style.height = 28 * 1.15f; title.style.unityTextAlign = UnityEngine.TextAnchor.MiddleLeft; title.style.flexShrink = 0; title.style.marginBottom = 18; content.Add(title); // 700 28px/1.15
            foreach (var point in data[language][1])
            {
                var dot = v.Box(); dot.style.width = dot.style.height = 9; dot.style.marginTop = 8; dot.style.marginRight = 14; dot.style.flexShrink = 0; dot.style.rotate = new Rotate(45); v.Decorate(dot, "a1", 2);
                // font: 700 16px/1.55 Baloo 2; text-wrap: pretty.
                var text = MenuView.CssParagraph(PanelUi.Wrap(v, point.ToString(), 16, "#E6E8F4"), 16 * 1.55f, true); PanelUi.Grow(text); text.style.flexBasis = 0; text.style.flexShrink = 1;
                var row = v.Row(dot, text); row.style.minWidth = 0; row.style.alignSelf = Align.Stretch; row.style.alignItems = Align.FlexStart; row.style.marginBottom = 12; content.Add(row);
            }
            var tiles = (JArray)data["tiles"];
            if (tiles.Count > 0)
            {
                content.Add(PanelUi.Spacer(v));
                var example = v.Box(); PanelUi.Pad(example, 18, 20); example.style.flexShrink = 0; v.Decorate(example, "dp", 18);
                var exampleLabel = v.Text(v.L("ÖRNEK", "EXAMPLE"), 11, false, "mt"); exampleLabel.style.unityFont = v.Font("Baloo2-800"); MenuView.Tracking(exampleLabel, 1.5f, 11); example.Add(exampleLabel);
                var row = v.Row(); row.style.marginTop = 14; row.style.marginBottom = 20; row.style.alignItems = Align.FlexEnd;
                foreach (var code in tiles)
                {
                    if (code.ToString() == "|") { var gap = v.Box(); gap.style.width = 22; gap.style.marginRight = 8; row.Add(gap); }
                    else
                    {
                        var tile = v.Tile(code.ToString(), 46, 62); tile.style.marginRight = 8; row.Add(tile);
                    }
                }
                example.Add(row);
                example.Add(PanelUi.Wrap(v, (string)data[language][2], 14)); content.Add(example);
            }
            return root;
        }
    }
}
