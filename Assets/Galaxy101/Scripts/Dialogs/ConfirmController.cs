using System.Globalization;
using UnityEngine.UIElements;

namespace Galaxy101
{
    public sealed class ConfirmController
    {
        public string Kind { get; set; } = "logout";
        private string confirmation = "";
        public void OnOpen() => confirmation = "";

        public static bool CanDelete(string typed, string language)
        {
            var en = language == "en";
            return (typed ?? "").Trim().ToUpper(CultureInfo.GetCultureInfo(en ? "en-US" : "tr-TR")) == (en ? "DELETE" : "SİL");
        }

        public VisualElement Build(MenuView v)
        {
            // Reference: 480px card, padding 30px 30px 24px, centered column with 12px gaps.
            var root = PanelUi.Shell(v, "Confirm"); root.style.paddingTop = 30; root.style.paddingLeft = root.style.paddingRight = 30; root.style.paddingBottom = 24;
            root.Q<VisualElement>("header").style.display = DisplayStyle.None;
            var body = root.Q<VisualElement>("content"); body.style.alignItems = Align.Center;
            var delete = Kind == "delete";
            var icon = v.Box(); icon.style.width = icon.style.height = 72; icon.style.alignItems = Align.Center; icon.style.justifyContent = Justify.Center;
            v.Decorate(icon, delete ? "linear-gradient(160deg,#FF7A86,#E23B4C)" : "linear-gradient(160deg,var(--pnH),var(--pn))", 22, 5, delete ? "#A82434" : "pnS");
            if (delete) GalaxyFilters.Shadow(icon, 0, 0, 15, v.C("rgba(255,94,108,.4)"));
            icon.Add(v.Icon(delete ? "delete" : "logout", 34, "#fff")); body.Add(icon);
            var title = v.Text(delete ? v.L("Hesabını silmek istiyor musun?", "Delete your account?") : v.L("Çıkış yapmak istiyor musun?", "Log out?"), 24, true);
            title.style.unityTextAlign = UnityEngine.TextAnchor.MiddleCenter; title.style.whiteSpace = WhiteSpace.Normal; title.style.marginTop = 18; body.Add(title);
            var desc = PanelUi.Wrap(v, delete ? v.L("Hesabın 30 gün sonra kalıcı olarak silinir. Bu süre içinde giriş yaparsan silme işlemi iptal olur.", "Your account will be deleted after 30 days. Logging in during that time cancels the deletion.") : v.L("Çiplerin, ilerlemen ve arkadaşların Google hesabında saklanır. İstediğin zaman tekrar giriş yapabilirsin.", "Your chips, progress and friends are saved to your Google account. You can log back in anytime."), 14);
            desc.style.marginTop = 12; desc.style.maxWidth = 380; desc.style.unityTextAlign = UnityEngine.TextAnchor.MiddleCenter; body.Add(desc);
            TextField field = null;
            if (delete)
            {
                var section = v.Column(); section.style.alignSelf = Align.Stretch; section.style.marginTop = 18;
                // Losses: padding 12px 14px, radius 14, rgba(255,94,108,.1) inside a 1px rgba(255,94,108,.3) ring; rows gap 6, 6px dot, gap 10, 700 13px #FFC2C8.
                var losses = v.Column(); PanelUi.Pad(losses, 11, 13); v.Decorate(losses, "rgba(255,94,108,.1)", 14);
                losses.style.borderLeftWidth = losses.style.borderRightWidth = losses.style.borderTopWidth = losses.style.borderBottomWidth = 1;
                losses.style.borderLeftColor = losses.style.borderRightColor = losses.style.borderTopColor = losses.style.borderBottomColor = v.C("rgba(255,94,108,.3)");
                var items = new[] { v.L("125.400 çip ve 340 elmas", "125,400 chips and 340 gems"), v.L("Seviye 24, Altın II derecesi ve rozetler", "Level 24, Gold II rank and badges"), v.L("Arkadaş listesi ve maç geçmişi", "Friend list and match history") };
                for (var i = 0; i < items.Length; i++)
                {
                    var dot = PanelUi.Circle(v, 6, "#FF5E6C"); dot.style.marginRight = 10;
                    var loss = v.Row(dot, PanelUi.Wrap(v, items[i], 13, "#FFC2C8")); if (i > 0) loss.style.marginTop = 6; losses.Add(loss);
                }
                section.Add(losses);
                var word = v.L("SİL", "DELETE");
                var label = v.Text(v.L("Onaylamak için " + word + " yaz", "Type " + word + " to confirm"), 12, false, "mt"); label.style.unityFont = v.Font("Baloo2-800"); label.style.marginTop = 12; label.style.marginBottom = 8; section.Add(label);
                field = PanelUi.Input(v, "delete-confirmation", confirmation, word, -1, 16, true); MenuView.Tracking(field, 2, 16); field.style.height = 46; field.style.flexGrow = 0; field.style.width = Length.Percent(100); section.Add(field);
                body.Add(section);
            }
            // Buttons: equal halves, gap 12, padding 13px 0, 700 16px.
            var footer = root.Q<VisualElement>("footer"); footer.style.borderTopWidth = 0; footer.style.marginTop = 22; footer.style.paddingTop = 0;
            var cancel = PanelUi.CssButton(v, v.L("Vazgeç", "Cancel"), v.CloseTop, "secondary", 13, 0, 16); cancel.style.flexGrow = 1; cancel.style.flexBasis = 0; cancel.style.flexShrink = 1; footer.Add(cancel);
            var confirm = PanelUi.CssButton(v, delete ? v.L("Hesabı sil", "Delete account") : v.L("Çıkış yap", "Log out"), () =>
            {
                if (delete && !CanDelete(field.value, v.Loc.Language)) return;
                if (delete ? v.Services.Profile.RequestDelete() : v.Services.Profile.Logout()) v.OpenDialog("bye");
            }, "danger", 13, 0, 16);
            confirm.name = "confirm-account-action"; confirm.style.marginLeft = 12; confirm.style.flexGrow = 1; confirm.style.flexBasis = 0; confirm.style.flexShrink = 1; footer.Add(confirm);
            if (delete)
            {
                void Validate(string value)
                {
                    var match = CanDelete(value, v.Loc.Language);
                    confirm.SetEnabled(match); confirm.style.opacity = 1;
                    v.Decorate(confirm, match ? "#FF5E6C" : "dp", 14, match ? 4 : 0, "#C23E4A");
                    confirm.style.color = v.C(match ? "#fff" : "mt");
                    field.EnableInClassList("invalid", match);
                }
                Validate(field.value);
                field.RegisterValueChangedCallback(evt => { confirmation = evt.newValue; Validate(evt.newValue); });
            }
            return root;
        }
    }
}
