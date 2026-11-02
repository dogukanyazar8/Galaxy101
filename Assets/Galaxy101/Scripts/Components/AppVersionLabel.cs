using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    /// <summary>
    /// "Galaxy 101 · vX" in the bottom-right corner of every screen but the splash and the main menu (prototype
    /// app.version: right 8px, bottom 3px, z 90, 700 10px Nunito, letter-spacing .5px, #9DA6D8, opacity .55; showVer).
    /// </summary>
    public static class AppVersionLabel
    {
        public const string Name = "app.version";

        public static VisualElement Attach(VisualElement host)
        {
            var existing = host.Q<VisualElement>(Name);
            if (existing != null) { existing.BringToFront(); return existing; }
            // Two runs as in the page ("Galaxy 101 · " and {{ appVersion }}), one line.
            var row = new VisualElement { name = Name, pickingMode = PickingMode.Ignore };
            var s = row.style;
            s.position = Position.Absolute; s.right = 8; s.bottom = 3; s.opacity = .55f; s.flexDirection = FlexDirection.Row;
            var font = Resources.Load<Font>("Galaxy101/Fonts/Nunito-700") ?? Resources.Load<Font>("Galaxy101/Fonts/Nunito-900");
            foreach (var text in new[] { "Galaxy 101 · ", "v" + UnityEngine.Application.version })
            {
                var label = new Label(text) { pickingMode = PickingMode.Ignore };
                var ls = label.style;
                ls.fontSize = 10;
                MenuView.Tracking(label, .5f, 10);   // letter-spacing .5px in this project's tracking units
                ls.color = new Color(0x9D / 255f, 0xA6 / 255f, 0xD8 / 255f);
                ls.paddingLeft = ls.paddingRight = ls.paddingTop = ls.paddingBottom = 0; ls.marginLeft = ls.marginRight = ls.marginTop = ls.marginBottom = 0;
                if (font != null) ls.unityFontDefinition = FontDefinition.FromFont(font);
                row.Add(label);
            }
            host.Add(row);
            return row;
        }
    }
}
