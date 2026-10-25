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

        public static Label Attach(VisualElement host)
        {
            var existing = host.Q<Label>(Name);
            if (existing != null) { existing.BringToFront(); return existing; }
            var label = new Label("Galaxy 101 · v" + UnityEngine.Application.version) { name = Name, pickingMode = PickingMode.Ignore };
            var s = label.style;
            s.position = Position.Absolute; s.right = 8; s.bottom = 3; s.fontSize = 10; s.opacity = .55f;
            MenuView.Tracking(label, .5f, 10);   // letter-spacing .5px in this project's tracking units
            s.color = new Color(0x9D / 255f, 0xA6 / 255f, 0xD8 / 255f);
            s.paddingLeft = s.paddingRight = s.paddingTop = s.paddingBottom = 0; s.marginLeft = s.marginRight = s.marginTop = s.marginBottom = 0;
            var font = Resources.Load<Font>("Galaxy101/Fonts/Nunito-700") ?? Resources.Load<Font>("Galaxy101/Fonts/Nunito-900");
            if (font != null) s.unityFontDefinition = FontDefinition.FromFont(font);
            host.Add(label);
            return label;
        }
    }
}
