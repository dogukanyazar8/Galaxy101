using System;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace Galaxy101
{
    public sealed class EditProfileController
    {
        private JObject draft;
        private static readonly string[] Colors = { "#FF8A5B", "#FF5E6C", "#E85D9A", "#8B5CF6", "#3D7BFF", "#16B6A8", "#3CCB6E", "#F5A623" };

        public static bool IsNameValid(string name)
        {
            var value = (name ?? "").Trim();
            return value.Length >= 3 && value.Length <= 16 && Regex.IsMatch(value, @"^[\p{L}\p{N} ._]+$");
        }

        public void OnOpen(MenuView v) { draft = v.Services.Profile.Get(); }

        public VisualElement Build(MenuView v)
        {
            if (draft == null) OnOpen(v);
            var root = PanelUi.Shell(v, "EditProfile");
            PanelUi.Header(v, root, v.L("Profili düzenle", "Edit profile"));
            var content = root.Q<VisualElement>("content"); content.style.flexDirection = FlexDirection.Row;
            var left = v.Box("g-edit-preview"); left.style.marginRight = 24;
            var preview = v.Box(); preview.name = "profile-preview"; preview.style.alignItems = Align.Center; left.Add(preview);
            var caption = v.Text(v.L("AVATAR RENGİ", "AVATAR COLOR"), 10, false, "mt"); caption.style.unityFont = v.Font("Baloo2-800"); MenuView.Tracking(caption, 1.4f, 10); caption.style.marginTop = 24; caption.style.marginBottom = 10; left.Add(caption);
            var colors = v.Row(); colors.name = "profile-colors"; colors.style.flexWrap = Wrap.Wrap; colors.style.width = 148; left.Add(colors); content.Add(left);
            var form = v.Column(); PanelUi.Grow(form); content.Add(form);
            var nameCount = v.Text("", 12, false, "mt");
            nameCount.style.unityFont = v.Font("Baloo2-800");
            Label FieldLabel(string text) { var l = v.Text(text, 12, false, "mt"); l.style.unityFont = v.Font("Baloo2-800"); return l; }
            var nameHeader = v.Row(FieldLabel(v.L("Oyuncu adı", "Player name")), PanelUi.Spacer(v), nameCount); nameHeader.style.marginBottom = 6; form.Add(nameHeader);
            var nameField = PanelUi.Input(v, "profile-name", (string)draft["name"], null, 16, 17, true); nameField.style.flexGrow = 0; form.Add(nameField);
            var hint = PanelUi.Wrap(v, "", 12); hint.name = "profile-name-hint"; hint.style.marginTop = 6; hint.style.marginBottom = 14; form.Add(hint);
            form.Add(FieldLabel(v.L("Durum mesajı", "Status message")));
            var statusField = PanelUi.Input(v, "profile-status", (string)draft["status"], v.L("Örn. Hızlı bir el için hep hazırım", "e.g. Always up for a quick game"), 40); statusField.style.marginTop = 6; statusField.style.marginBottom = 14; statusField.style.flexGrow = 0; form.Add(statusField);
            form.Add(FieldLabel(v.L("Avatar çerçevesi", "Avatar frame")));
            var frames = v.Row(); frames.AddToClassList("g-segment"); frames.style.marginTop = 6; frames.style.alignSelf = Align.FlexStart; form.Add(frames);
            var footer = root.Q<VisualElement>("footer");
            // Reference footer: full-width band, padding 14px 24px, rgba(0,0,0,.12) over a 1px top rule.
            footer.style.marginLeft = footer.style.marginRight = -24; footer.style.marginBottom = -24; footer.style.marginTop = 22;
            footer.style.paddingTop = footer.style.paddingBottom = 14; footer.style.paddingLeft = footer.style.paddingRight = 24;
            footer.style.backgroundColor = v.C("rgba(0,0,0,.12)"); footer.style.borderBottomLeftRadius = footer.style.borderBottomRightRadius = 25; footer.style.alignItems = Align.Center;
            var note = PanelUi.Wrap(v, v.L("Adını 30 günde bir değiştirebilirsin.", "You can change your name once every 30 days."), 12); PanelUi.Grow(note); footer.Add(note);
            footer.Add(PanelUi.CssButton(v, v.L("Vazgeç", "Cancel"), v.CloseTop, "secondary", 12, 22, 16));
            var save = PanelUi.CssButton(v, v.L("Kaydet", "Save"), () =>
            {
                if (!IsNameValid((string)draft["name"])) return;
                if (v.Services.Profile.Save(((string)draft["name"]).Trim(), (string)draft["color"], (string)draft["status"], (int)draft["frame"])) { draft = null; v.CloseTop(); }
                else PanelUi.SaveFailure(v);
            }, "primary", 12, 28, 16);
            save.name = "profile-save"; save.style.marginLeft = 12; footer.Add(save);
            void Preview()
            {
                preview.Clear(); var avatar = v.Avatar(110, draft, true);
                var level = avatar.Q<Label>(className: "avatar-level"); if (level != null) { level.text = v.L("SV 24", "LV 24"); level.style.fontSize = 11; }
                preview.Add(avatar);
                foreach (var child in colors.Children()) child.EnableInClassList("selected", child.name == (string)draft["color"]);
                for (var i = 0; i < frames.childCount; i++)
                {
                    frames[i].EnableInClassList("selected", i == (int)draft["frame"]);
                    if (i == (int)draft["frame"]) v.Decorate(frames[i], "linear-gradient(var(--a1),var(--a2))", 10, 3, "aS");
                    else v.Decorate(frames[i], "transparent", 10);
                }
                var valid = IsNameValid((string)draft["name"]);
                nameCount.text = ((string)draft["name"] ?? "").Trim().Length + "/16";
                nameField.EnableInClassList("invalid", !valid); save.SetEnabled(valid);
                save.style.opacity = 1;
                v.Decorate(save, valid ? "linear-gradient(var(--a1),var(--a2))" : "dp", 14, valid ? 4 : 0, "aS");
                save.style.color = v.C(valid ? "aT" : "mt");
                hint.text = valid ? v.L("Masadaki diğer oyuncular bu adı görür.", "Other players see this name at the table.") : v.L("3–16 karakter: harf, rakam, boşluk, nokta veya alt çizgi.", "3–16 characters: letters, numbers, space, dot or underscore.");
                hint.style.color = v.C(valid ? "mt" : "#FF8A94");
            }
            foreach (var hex in Colors)
            {
                var color = hex;
                var button = PanelUi.CustomButton(v, "", () => { draft["color"] = color; Preview(); }, "g-edit-swatch"); button.name = color; button.style.backgroundColor = v.C(color); colors.Add(button);
            }
            for (var i = 0; i < 3; i++)
            {
                var index = i;
                var text = v.L(new[] { "Klasik", "Altın", "Galaksi · SV 30" }[i], new[] { "Classic", "Gold", "Galaxy · LV 30" }[i]);
                var locked = i == 2;
                var button = PanelUi.CustomButton(v, locked ? "" : text, () => { if (index < 2) { draft["frame"] = index; Preview(); } }, "g-segment-button");
                button.name = "draft-frame-" + i; if (i < 2) button.style.marginRight = 4;
                if (locked)
                {
                    // Reference: 12px lock (stroke currentColor), gap 7, label in rgba(255,255,255,.35); not dimmed further.
                    button.SetEnabled(false); button.style.opacity = 1; button.style.flexDirection = FlexDirection.Row; button.style.alignItems = Align.Center;
                    var lockIcon = v.Icon("lock", 12, "rgba(255,255,255,.35)"); lockIcon.name = "draft-frame-lock"; lockIcon.style.marginRight = 7; button.Add(lockIcon);
                    var label = v.Text(text, 14, true, "rgba(255,255,255,.35)"); label.style.whiteSpace = WhiteSpace.NoWrap; button.Add(label);
                }
                frames.Add(button);
            }
            nameField.RegisterValueChangedCallback(evt => { draft["name"] = evt.newValue; Preview(); });
            statusField.RegisterValueChangedCallback(evt => draft["status"] = evt.newValue);
            Preview(); return root;
        }
    }
}
