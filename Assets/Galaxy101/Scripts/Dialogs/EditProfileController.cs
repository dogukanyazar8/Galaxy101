using System;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace Galaxy101
{
    public sealed class EditProfileController
    {
        private JObject draft;
        // Each opening of the dialog; a save that returns after the dialog was closed or opened again leaves it alone.
        private int opening, savingOpening = -1;
        // Only the opening that sent a save waits for it: a reopened dialog is not blocked by an older request.
        private bool Saving => savingOpening == opening;
        private static readonly string[] Colors = { "#FF8A5B", "#FF5E6C", "#E85D9A", "#8B5CF6", "#3D7BFF", "#16B6A8", "#3CCB6E", "#F5A623" };

        public static bool IsNameValid(string name)
        {
            var value = (name ?? "").Trim();
            return value.Length >= 3 && value.Length <= 16 && Regex.IsMatch(value, @"^[\p{L}\p{N} ._]+$");
        }

        public void OnOpen(MenuView v) { draft = v.Services.Profile.Get(); opening++; }

        public VisualElement Build(MenuView v)
        {
            if (draft == null) OnOpen(v);
            var root = PanelUi.Shell(v, "EditProfile");
            // Header: padding 20px 24px 6px, 700 24px/1.15 title, 40px close; body padding 14px 24px 22px.
            // The sections carry their own padding: the dialog's page clips at the card edge, so the full-width footer band
            // cannot reach out of a padded root with negative margins (that cut off its note and the buttons' bottoms).
            root.style.paddingTop = root.style.paddingBottom = root.style.paddingLeft = root.style.paddingRight = 0;
            PanelUi.Header(v, root, v.L("Profili düzenle", "Edit profile"));
            var header = root.Q<VisualElement>("header"); header.style.marginBottom = 0; header.style.paddingTop = 20; header.style.paddingBottom = 6; header.style.paddingLeft = header.style.paddingRight = 24;
            header.Q<Label>().name = "menu.pf.title"; header.Q<Button>("dialog-close").name = "menu.closeDlg#2";
            var content = root.Q<VisualElement>("content"); content.style.flexDirection = FlexDirection.Row; content.style.alignItems = Align.Stretch;
            content.style.paddingTop = 14; content.style.paddingBottom = 22; content.style.paddingLeft = content.style.paddingRight = 24;
            // Preview column: 220 px, centred, gap 14, padding 18px 14px, radius 20, --sd.
            var left = v.Box("g-edit-preview"); left.style.marginRight = 24; left.style.paddingLeft = left.style.paddingRight = 14;
            var preview = v.Box(); preview.name = "profile-preview"; preview.style.alignItems = Align.Center; left.Add(preview);
            var caption = v.Text(v.L("AVATAR RENGİ", "AVATAR COLOR"), 10, false, "mt"); caption.name = "menu.pf.colorLabel"; caption.style.unityFont = v.Font("Baloo2-800"); MenuView.Tracking(caption, 1.4f, 10); caption.style.marginTop = 20; caption.style.marginBottom = 14; left.Add(caption);
            // grid repeat(4, 28px), gap 8.
            var colors = v.Column(); colors.name = "profile-colors"; left.Add(colors); content.Add(left);
            var form = v.Column(); PanelUi.Grow(form); content.Add(form);
            var nameCount = MenuView.TabularDigits(v.Text("", 12, false, "mt")); nameCount.name = "menu.pf.count";
            nameCount.style.unityFont = v.Font("Baloo2-800");
            Label FieldLabel(string text, string id) { var l = v.Text(text, 12, false, "mt"); l.name = id; l.style.unityFont = v.Font("Baloo2-800"); return MenuView.CssLine(l); }
            var nameHeader = v.Row(FieldLabel(v.L("Oyuncu adı", "Player name"), "menu.pf.nameLabel"), PanelUi.Spacer(v), nameCount); nameHeader.style.marginBottom = 6; form.Add(nameHeader);
            var nameField = PanelUi.Input(v, "menu.pf.onName", (string)draft["name"], null, 16, 17, true); nameField.style.flexGrow = 0; form.Add(nameField);
            var hint = MenuView.CssLine(v.Text("", 12, false, "mt")); hint.name = "menu.pf.hint"; hint.style.marginTop = 6; hint.style.marginBottom = 14; form.Add(hint);
            form.Add(FieldLabel(v.L("Durum mesajı", "Status message"), "menu.pf.statusLabel"));
            var statusField = PanelUi.Input(v, "menu.pf.onStatus", (string)draft["status"], v.L("Örn. Hızlı bir el için hep hazırım", "e.g. Always up for a quick game"), 40); statusField.style.marginTop = 6; statusField.style.marginBottom = 14; statusField.style.flexGrow = 0; form.Add(statusField);
            form.Add(FieldLabel(v.L("Avatar çerçevesi", "Avatar frame"), "menu.pf.frameLabel"));
            // grid repeat(7, minmax(0,1fr)), gap 6, padding 6, radius 14, --dp.
            var frames = v.Row(); frames.name = "profile-frames"; frames.style.marginTop = 6; frames.style.alignItems = Align.Stretch; PanelUi.Pad(frames, 6, 6); v.Decorate(frames, "dp", 14); form.Add(frames);
            var footer = root.Q<VisualElement>("footer");
            // Reference footer: full-width band, padding 14px 24px, rgba(0,0,0,.12) over a 1px top rule.
            footer.style.marginLeft = footer.style.marginRight = footer.style.marginBottom = footer.style.marginTop = 0;
            footer.style.paddingTop = footer.style.paddingBottom = 14; footer.style.paddingLeft = footer.style.paddingRight = 24;
            footer.style.backgroundColor = v.C("rgba(0,0,0,.12)"); footer.style.alignItems = Align.Center;
            var note = PanelUi.Wrap(v, v.L("Adını 30 günde bir değiştirebilirsin.", "You can change your name once every 30 days."), 12); note.name = "menu.pf.note"; PanelUi.Grow(note); footer.Add(note);
            var cancel = PanelUi.CssButton(v, v.L("Vazgeç", "Cancel"), v.CloseTop, "secondary bright-120", 12, 22, 16); cancel.name = "menu.closeDlg#3"; footer.Add(cancel);
            Button save = null;
            // The dialog closes once the profile is saved (on the server too when signed in); a failure keeps the draft.
            // Only the opening that sent the save is closed: the player may have cancelled and opened something else.
            async void Save()
            {
                if (Saving || !IsNameValid((string)draft["name"])) return;
                var sent = opening; savingOpening = sent; save.SetEnabled(false);
                string error;
                // The saved frame keeps its v1 value for Altın (frameOf: 1 → 12); other league frames cannot be stored yet (EKSIKLER EK-603).
                int frame = MenuView.FrameId((int)draft["frame"]); int stored = frame == 12 ? 1 : frame;
                try { error = await v.Services.Profile.SaveAsync(((string)draft["name"]).Trim(), (string)draft["color"], (string)draft["status"], stored); }
                finally { if (savingOpening == sent) savingOpening = -1; }
                bool current = sent == opening && v.Modals.Dialog == "profile";
                if (error == null) { if (current) { draft = null; v.CloseTop(); } return; }
                if (!current) return;
                (v.Root.Q<Button>("menu.pf.save") ?? save).SetEnabled(true); // a rebuild during the save made a new button
                if (error == "offline") v.Flash(v.L("Sunucuya ulaşılamadı, profil kaydedilmedi", "Server unreachable: the profile was not saved"), "bad");
                else if (error == "inappropriate") v.Flash(v.L("Bu ad ya da durum mesajı uygun değil", "This name or status is not allowed"), "bad");
                else PanelUi.SaveFailure(v);
            }
            save = PanelUi.CssButton(v, v.L("Kaydet", "Save"), Save, "primary bright-120", 12, 28, 16);
            save.name = "menu.pf.save"; save.style.marginLeft = 12; footer.Add(save);
            int peak = v.PeakRankIndex();
            void Preview()
            {
                preview.Clear(); var avatar = v.Avatar(110, draft, true);
                avatar.Q<Label>("avatar-initials").name = "menu.pf.ini"; avatar.Q<Label>(className: "avatar-level").name = "menu.pf.lv";
                preview.Add(avatar);
                foreach (var swatch in colors.Query<Button>(className: "g-edit-swatch").ToList()) swatch.Q("swatch-ring").style.display = (string)swatch.userData == (string)draft["color"] ? DisplayStyle.Flex : DisplayStyle.None;
                frames.Clear(); BuildFrames(v, frames, peak, Preview);
                var valid = IsNameValid((string)draft["name"]);
                nameCount.text = ((string)draft["name"] ?? "").Trim().Length + "/16"; MenuView.TabularDigits(nameCount);
                nameField.EnableInClassList("invalid", !valid); save.SetEnabled(valid && !Saving);
                save.style.opacity = 1;
                v.Decorate(save, valid ? "linear-gradient(var(--a1),var(--a2))" : "dp", 14, valid ? 4 : 0, "aS");
                save.style.color = v.C(valid ? "aT" : "mt");
                hint.text = valid ? v.L("Masadaki diğer oyuncular bu adı görür.", "Other players see this name at the table.") : v.L("3–16 karakter: harf, rakam, boşluk, nokta veya alt çizgi.", "3–16 characters: letters, numbers, space, dot or underscore.");
                hint.style.color = v.C(valid ? "mt" : "#FF8A94");
            }
            VisualElement swatchRow = null;
            for (var i = 0; i < Colors.Length; i++)
            {
                var color = Colors[i];
                if (i % 4 == 0) { swatchRow = v.Row(); if (i > 0) swatchRow.style.marginTop = 8; colors.Add(swatchRow); }
                var button = PanelUi.CustomButton(v, "", () => { draft["color"] = color; Preview(); }, "g-edit-swatch"); button.name = "menu.c.pick[" + i + "]"; button.userData = color;
                button.style.marginLeft = button.style.marginTop = button.style.marginBottom = 0; button.style.marginRight = i % 4 < 3 ? 8 : 0; button.style.borderLeftWidth = button.style.borderRightWidth = button.style.borderTopWidth = button.style.borderBottomWidth = 0;
                v.Decorate(button, color, 14);
                // Selected: box-shadow 0 0 0 3px var(--sd), 0 0 0 5px #fff — a white ring 3px off the swatch.
                var ring = v.Box(); ring.name = "swatch-ring"; ring.pickingMode = PickingMode.Ignore; ring.style.position = Position.Absolute; ring.style.left = ring.style.right = ring.style.top = ring.style.bottom = 0;
                var white = PanelUi.Ring(v, 2, "#fff", 19); white.style.left = white.style.right = white.style.top = white.style.bottom = -5; ring.Add(white);
                var gap = PanelUi.Ring(v, 3, "sd", 17); gap.style.left = gap.style.right = gap.style.top = gap.style.bottom = -3; ring.Add(gap);
                button.Add(ring); swatchRow.Add(button);
            }
            nameField.RegisterValueChangedCallback(evt => { draft["name"] = evt.newValue; Preview(); });
            statusField.RegisterValueChangedCallback(evt => draft["status"] = evt.newValue);
            Preview(); return root;
        }

        /// <summary>
        /// pf.frames (frameOpts): Klasik and the six league frames; a tile is a 40px chip over its name (700 11px), picked
        /// with --pnH and an inset 2px --a1 ring, locked above the season's peak league (opacity .5, a lock badge and the
        /// tip "Derecelide {lig} ligine ulaşınca açılır").
        /// </summary>
        private void BuildFrames(MenuView v, VisualElement frames, int peak, Action changed)
        {
            bool en = v.Loc.Language == "en"; int current = MenuView.FrameId((int)draft["frame"]);
            for (var i = 0; i < MenuView.FrameIds.Length; i++)
            {
                int id = MenuView.FrameIds[i]; bool on = id == current, locked = MenuView.FrameLocked(id, peak);
                var league = id >= 10 ? (JObject)MenuView.Leagues[id - 10] : null; string name = league == null ? v.L("Klasik", "Classic") : (string)league[en ? "en" : "tr"];
                var tile = PanelUi.CustomButton(v, "", () => { if (locked) return; draft["frame"] = id; changed(); }, "g-frame-option bright-100"); tile.name = "menu.o.pick[" + i + "]";
                tile.style.flexGrow = 1; tile.style.flexBasis = 0; tile.style.minWidth = 0; tile.style.flexDirection = FlexDirection.Column; tile.style.alignItems = Align.Center; tile.style.justifyContent = Justify.FlexStart;
                tile.style.paddingTop = 8; tile.style.paddingBottom = 6; tile.style.paddingLeft = tile.style.paddingRight = 2; if (i < MenuView.FrameIds.Length - 1) tile.style.marginRight = 6;
                v.Decorate(tile, on ? "pnH" : "transparent", 10); if (on) tile.Add(PanelUi.Ring(v, 2, "a1", 10));
                if (!on) { tile.RegisterCallback<PointerEnterEvent>(_ => v.Decorate(tile, "pnH", 10)); tile.RegisterCallback<PointerLeaveEvent>(_ => v.Decorate(tile, "transparent", 10)); }
                if (locked) { tile.SetEnabled(false); tile.style.opacity = .5f; tile.tooltip = en ? "Reach " + (string)league["en"] + " in ranked to unlock" : "Derecelide " + (string)league["tr"] + " ligine ulaşınca açılır"; }
                var chip = v.Avatar(40, new JObject { ["name"] = draft["name"], ["color"] = draft["color"], ["frame"] = id });
                if (locked)
                {
                    var badge = v.Box(); badge.pickingMode = PickingMode.Ignore; badge.style.position = Position.Absolute; badge.style.right = badge.style.bottom = -3; badge.style.width = badge.style.height = 18; badge.style.alignItems = Align.Center; badge.style.justifyContent = Justify.Center; v.Decorate(badge, "pn", 9);
                    badge.Add(v.Icon("lock", 10, "mt")); chip.Add(badge);
                }
                tile.Add(chip);
                var label = v.Text(name, 11, true, on ? "#F4F1E8" : "mt"); label.name = "menu.o.label[" + i + "]"; label.style.marginTop = 4; label.style.whiteSpace = WhiteSpace.NoWrap; tile.Add(label);
                frames.Add(tile);
            }
        }
    }
}
