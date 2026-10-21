using UnityEngine.UIElements;

namespace Galaxy101
{
    public sealed class DoneController
    {
        public string From { get; set; } = "logout";
        public VisualElement Build(MenuView v)
        {
            // Reference: 420px card, padding 34px 30px 26px, centered column with 10px gaps.
            var root = PanelUi.Shell(v, "Done"); root.style.paddingTop = 34; root.style.paddingLeft = root.style.paddingRight = 30; root.style.paddingBottom = 26;
            root.Q<VisualElement>("header").style.display = DisplayStyle.None;
            root.Q<VisualElement>("footer").style.display = DisplayStyle.None;
            var body = root.Q<VisualElement>("content"); body.style.alignItems = Align.Center;
            var icon = PanelUi.Circle(v, 64, "#3CCB6E"); v.Decorate(icon, "#3CCB6E", 32, 5, "#22914A"); GalaxyFilters.Shadow(icon, 0, 0, 15, v.C("rgba(60,203,110,.5)"));
            icon.Add(v.Icon("tick", 30, "#fff")); body.Add(icon);
            var title = v.Text(From == "delete" ? v.L("Silme talebin alındı", "Deletion scheduled") : v.L("Çıkış yapıldı", "Logged out"), 22, true); title.style.unityTextAlign = UnityEngine.TextAnchor.MiddleCenter; title.style.marginTop = 16; body.Add(title);
            var description = PanelUi.Wrap(v, From == "delete" ? v.L("30 gün içinde giriş yaparak iptal edebilirsin.", "Log in within 30 days to cancel.") : v.L("Masada görüşmek üzere.", "See you at the table soon."), 14); description.style.marginTop = 10; description.style.unityTextAlign = UnityEngine.TextAnchor.MiddleCenter; body.Add(description);
            var done = PanelUi.CssButton(v, v.L("Tamam", "OK"), v.CloseTop, "primary", 12, 30, 16); done.style.marginTop = 20; body.Add(done); return root;
        }
    }
}
