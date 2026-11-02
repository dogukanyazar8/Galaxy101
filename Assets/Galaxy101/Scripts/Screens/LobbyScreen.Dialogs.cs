using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Galaxy101.Contracts;
using Galaxy101.Online;
using UnityEngine;
using UnityEngine.UIElements;

namespace Galaxy101
{
    public sealed partial class LobbyScreen
    {
        // ---------- dialogs (dlgOpen: z 30, lbFade .18s, backdrop rgba(0,0,0,.55) + blur(6px)) ----------
        public sealed class Dialog
        {
            /// <summary>connect, pin, code, full, closed, share — and Unity's own offline / invited (no prototype design, EK-806).</summary>
            public string Kind, Pin = "", Code = "", CodeError;
            public TableSummary Table;
            public InviteMessage Invite;
            /// <summary>A v4 invite (inviteIn): answered with inviteRespond.</summary>
            public IncomingInvite Incoming;
            public float Shake, OpenedAt;
            /// <summary>d.err of the PIN dialog: the PIN was wrong.</summary>
            public bool Error;
            public bool First, Copied, Shared;
            /// <summary>The PIN asked for İzle (attempt(t, 'watch')), not to sit.</summary>
            public bool Watch;
            /// <summary>The share dialog's code and PIN (the room's, or the table just created).</summary>
            public string SharePin;
        }

        private Dialog dialog;
        public Dialog Current => dialog;
        private float shadeShownAt;

        private void OpenDialog(Dialog next) { next.OpenedAt = Time.realtimeSinceStartup; dialog = next; RenderDialog(); }
        private void CloseDialog() { dialog = null; RenderDialog(); }

        /// <summary>closeDlg: every dialog but "Masaya bağlanılıyor…" closes from the backdrop.</summary>
        private void CloseFromBackdrop() { if (dialog != null && dialog.Kind != "connect" && dialog.Kind != "offline") CloseDialog(); }

        /// <summary>A CSS content-box width: the design's "width" plus the padding and border already set on the card.</summary>
        private static void ContentWidth(VisualElement card, float width) =>
            card.style.width = width + card.style.paddingLeft.value.value + card.style.paddingRight.value.value + card.style.borderLeftWidth.value + card.style.borderRightWidth.value;

        /// <summary>openCode: dlg {kind: 'code', code: '', err: null} (S12).</summary>
        public void OpenCode() => OpenDialog(new Dialog { Kind = "code" });

        /// <summary>A friend's invite (realtime <c>invite</c>, v4 inviteIn): who, the stake; Katıl joins by the table code.</summary>
        public void ShowInvite(InviteMessage message) => OpenDialog(new Dialog { Kind = "invited", Invite = message });

        /// <summary>inviteIn while the lobby is open (outside a waiting room and the form): the same invite dialog.</summary>
        private void OnInviteReceived(IncomingInvite invite)
        {
            if (layer == null || invite == null || backend.Room != null) return;
            OpenDialog(new Dialog { Kind = "invited", Incoming = invite });
        }

        private void RenderDialog()
        {
            if (dialogLayer == null) return;
            var d = dialog; if (d == null) { dialogLayer.Clear(); dialogLayer.pickingMode = PickingMode.Ignore; return; }
            dialogLayer.pickingMode = PickingMode.Position;
            // The blurred backdrop is made once per dialog session and kept while dialogs follow each other (PIN → shake →
            // bağlanılıyor): only the card is rebuilt, so no element with a backdrop filter comes and goes during a change.
            var shade = dialogLayer.Q<VisualElement>("lobby.closeDlg");
            if (shade == null)
            {
                shade = Fill("lobby.closeDlg"); shade.pickingMode = PickingMode.Position; view.Decorate(shade, "rgba(0,0,0,.55)"); GalaxyFilters.BackdropBlur(shade, 6);
                Tap(shade, CloseFromBackdrop); dialogLayer.Add(shade);
                shadeShownAt = d.OpenedAt;
            }
            FadeShade(shade, shadeShownAt, .18f);
            dialogLayer.Q<VisualElement>("lobby-dialog-frame")?.RemoveFromHierarchy();
            var frame = Fill("lobby-dialog-frame"); frame.style.alignItems = Align.Center; frame.style.justifyContent = Justify.Center; dialogLayer.Add(frame);
            // Card: --pn, 1px rgba(255,255,255,.1), radius 26, 0 10px 0 --pnS, 0 40px 90px rgba(0,0,0,.6).
            var card = Box("lobby-dialog-" + d.Kind); card.pickingMode = PickingMode.Position; card.style.alignItems = Align.Center;
            Border(card, 1, new Color(1, 1, 1, .1f)); Raise(card, "var(--pn)", 26, 10, "var(--pnS)"); GalaxyFilters.BoxShadow(card, 0, 40, 45, new Color(0, 0, 0, .6f));
            card.RegisterCallback<ClickEvent>(e => e.StopPropagation()); frame.Add(card);
            // lbModal .25s (share .3s cubic-bezier(.2,1.2,.4,1)).
            if (d.Kind == "share") Pop(card, d.OpenedAt, .3f, true); else Pop(card, d.OpenedAt, .25f, false);
            switch (d.Kind)
            {
                case "connect": BuildConnect(card, d); break;
                case "pin": BuildPin(card, d); break;
                case "code": BuildCodeDialog(card, d); break;
                case "full": case "closed": BuildError(card, d); break;
                case "share": BuildShare(card, d); break;
                case "offline": BuildOffline(card); break;
                case "invited": BuildInvited(card, d); break;
            }
        }

        /// <summary>d.isConnect: 380 wide, padding 30, gap 12 — spinner 44 (4px --a1, lbSpin .8s), title, the table's name.</summary>
        private void BuildConnect(VisualElement card, Dialog d)
        {
            Pad(card, 30, 30, 30, 30); ContentWidth(card, 380);
            var spin = Spinner(44, "var(--a1)", 4); card.Add(spin);
            var title = Text(S("connT"), 20, "Montserrat-700", "#F4F1E8", 0, "lobby.d.title"); title.style.marginTop = 12; card.Add(title);
            var body = Text(d.Table?.Name ?? "", 14, "Baloo2-700", "var(--mt)", 0, "lobby.d.body"); body.style.marginTop = 12; card.Add(body);
        }

        /// <summary>d.isPass (S11): 440 wide, padding 30 30 24, gap 12 — lock badge, Şifreli masa, name · bet, four PIN boxes, error, Vazgeç / Gir.</summary>
        private void BuildPin(VisualElement card, Dialog d)
        {
            Pad(card, 30, 30, 24, 30); ContentWidth(card, 440);
            // 68 × 68, radius 22, linear-gradient(160deg,--a1,--a2), 0 5px 0 --aS, 0 0 30px --gl.
            var icon = Box(); Square(icon, 68); Raise(icon, "linear-gradient(160deg,var(--a1),var(--a2))", 22, 5, "var(--aS)"); GalaxyFilters.Shadow(icon, 0, 0, 15, view.C("var(--gl)")); icon.Add(view.Icon(IconLock, 30, "var(--aT)")); card.Add(icon);
            var title = Text(S("passT"), 24, "Montserrat-700", "#F4F1E8", 1.2f, "lobby.text.passT"); title.style.marginTop = 12; card.Add(title);
            // passBody(name, bet); a code join that needs a PIN knows only its code (the server sends no table, UF-805).
            var sub = Text(d.Table != null ? S("passBody", d.Table.Name, view.N(d.Table.Stake)) : d.Code, 14, "Baloo2-700", "var(--mt)", 0, "lobby.d.body#2"); sub.style.marginTop = 12; card.Add(sub);
            // Boxes: margin 8px 0 2px, gap 10; 58 × 66, radius 16, --dp, 700 30px.
            var boxes = Row(); boxes.name = "lobby-pin-boxes"; boxes.style.marginTop = 12 + 8; boxes.style.marginBottom = 2;
            var cells = new List<(Label Cell, Action<string> Ring, VisualElement Halo)>();
            for (int i = 0; i < 4; i++)
            {
                var slot = Box(); slot.style.width = 58; slot.style.height = 66; if (i > 0) slot.style.marginLeft = 10;
                // The focused box's "0 0 14px --gl" glow: a twin behind it whose opacity follows (the filter itself never changes).
                // The focused box's "0 0 14px --gl" glow as three widening outlines of falling strength: no filter (a filtered
                // twin inside the dialog's own filtered card stacked render passes the renderer released while typing).
                var halo = Box(); halo.style.position = Position.Absolute; halo.style.left = halo.style.top = halo.style.right = halo.style.bottom = 0; halo.style.display = DisplayStyle.None;
                foreach (var (reach, alpha) in new[] { (3f, .5f), (7f, .28f), (11f, .12f) })
                {
                    var glow = view.C("var(--gl)"); glow.a *= alpha;
                    var line = Box(); MenuView.Place(line, -reach, -reach, 58 + 2 * reach, 66 + 2 * reach); Radius(line, 16 + reach); Border(line, 4, glow); halo.Add(line);
                }
                slot.Add(halo);
                var cell = Text("", 30, "Montserrat-700", "#F4F1E8", 0, "lobby.p.ch[" + i + "]"); MenuView.Place(cell, 0, 0, 58, 66); cell.style.unityTextAlign = TextAnchor.MiddleCenter;
                view.Decorate(cell, "var(--dp)", 16); var ring = view.InsetRing(cell, 16); slot.Add(cell);
                cells.Add((cell, ring, halo)); boxes.Add(slot);
            }
            var field = Input("lobby.d.onPin", d.Pin, "", 16, "Baloo2-700", "#F4F1E8", 4);
            field.style.position = Position.Absolute; field.style.left = field.style.top = field.style.right = field.style.bottom = 0; field.style.opacity = 0;
            VisualElement submit = null;
            void Paint()
            {
                // p.ring: err → inset 2px #FF5E6C; the next box → inset 2px --a1 + 0 0 14px --gl; filled → inset 2px --bd; empty → inset 0 2px 0 rgba(0,0,0,.3).
                for (int i = 0; i < 4; i++)
                {
                    var (cell, ring, halo) = cells[i];
                    cell.text = i < d.Pin.Length ? "•" : "";
                    bool next = !d.Error && i == d.Pin.Length;
                    ring(d.Error ? "bad" : next ? "focus" : i < d.Pin.Length ? "line" : "rest");
                    halo.style.display = next ? DisplayStyle.Flex : DisplayStyle.None;
                }
                if (submit != null) PaintPinSubmit(submit, d.Pin.Length == 4);
            }
            field.RegisterValueChangedCallback(e =>
            {
                // onPin: digits only, at most four; typing clears the error. Gir (or Enter) sends it.
                string digits = Regex.Replace(e.newValue ?? "", "\\D", ""); if (digits.Length > 4) digits = digits.Substring(0, 4);
                if (digits != e.newValue) field.SetValueWithoutNotify(digits);
                d.Pin = digits; bool was = d.Error; d.Error = false; Paint(); if (was) ErrorLine(card, d);
            });
            field.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { e.StopPropagation(); SubmitPin(d); } }, TrickleDown.TrickleDown);
            boxes.Add(field); card.Add(boxes);
            Shake(boxes, d.Shake);
            ErrorLine(card, d);
            var buttons = Pair(S("cancel"), CloseDialog, S("enter"), () => SubmitPin(d), "lobby.closeDlg#2", "lobby.d.submitPin", d.Pin.Length == 4, 12);
            submit = buttons.Q("lobby.d.submitPin"); card.Add(buttons);
            Paint();
            field.schedule.Execute(() => field.Focus()).ExecuteLater(30);
        }

        /// <summary>d.pinBg: Gir turns gold once four digits are in, --pnH / --mt before.</summary>
        private void PaintPinSubmit(VisualElement button, bool ready)
        {
            view.Decorate(button, ready ? "linear-gradient(var(--a1),var(--a2))" : "var(--pnH)", 16); Ledge(button, 5, ready ? "var(--aS)" : "var(--pnS)");
            if (button is Label label) label.style.color = view.C(ready ? "var(--aT)" : "var(--mt)");
        }

        /// <summary>submitPin: nothing until four digits; then the server checks the PIN (JoinAsync).</summary>
        private void SubmitPin(Dialog d)
        {
            if (d.Pin.Length < 4) return;
            _ = JoinAsync(new TableJoinMessage { TableId = d.Table?.Id, Code = d.Table == null ? d.Code : null, Pin = d.Pin }, d.Table, d.Watch);
        }

        /// <summary>d.isCode (S12): 480 wide, padding 26 28 24, gap 14 — key badge and texts, the code field, the error line, Vazgeç / Katıl.</summary>
        private void BuildCodeDialog(VisualElement card, Dialog d)
        {
            card.style.alignItems = Align.Stretch; Pad(card, 26, 28, 24, 28); ContentWidth(card, 480);
            var head = Row();
            var icon = Box(); Square(icon, 46); Raise(icon, "var(--pnH)", 14, 4, "var(--pnS)"); icon.Add(view.Icon(IconKey, 22, "var(--a1)")); head.Add(icon);
            var words = Box(); words.style.marginLeft = 12; words.style.flexShrink = 1;
            words.Add(Text(S("codeT"), 22, "Montserrat-700", "#F4F1E8", 1.15f, "lobby.text.codeT"));
            words.Add(Text(S("codeS"), 13, "Baloo2-700", "var(--mt)", 0, "lobby.text.codeS"));
            head.Add(words); card.Add(head);
            // 62 tall, padding 0 18, radius 16, --dp; codeRing: err → inset 0 0 0 2px #FF5E6C, else inset 0 0 0 2px --bd.
            var box = Row(); box.name = "lobby-code-box"; box.style.height = 62; box.style.marginTop = 14; Pad(box, 0, 18, 0, 18); view.Decorate(box, "var(--dp)", 16);
            var ring = FieldRing(box, 16); ring(d.CodeError != null ? "bad" : "line");
            var field = Input("lobby.d.onCode", d.Code, "GX-0000", 26, "Montserrat-700", "var(--a1)", 8); field.style.flexGrow = 1; MenuView.Tracking(field, 5, 26);
            field.RegisterValueChangedCallback(e =>
            {
                // onCode: upper case, only A–Z 0–9 and "-", at most 7; typing clears the error.
                string up = Regex.Replace((e.newValue ?? "").ToUpperInvariant(), "[^A-Z0-9-]", ""); if (up.Length > 7) up = up.Substring(0, 7);
                if (up != e.newValue) field.SetValueWithoutNotify(up);
                d.Code = up; if (d.CodeError != null) { d.CodeError = null; ring("line"); ErrorLine(card, d); }
            });
            field.RegisterCallback<KeyDownEvent>(e => { if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) { e.StopPropagation(); SubmitCode(d); } }, TrickleDown.TrickleDown);
            box.Add(field); card.Add(box); Shake(box, d.Shake);
            ErrorLine(card, d);
            card.Add(Pair(S("cancel"), CloseDialog, S("join"), () => SubmitCode(d), "lobby.closeDlg#3", "lobby.d.submitCode", true, 14));
            field.schedule.Execute(() => field.Focus()).ExecuteLater(30);
        }

        /// <summary>
        /// submitCode's check: the code without dashes must be 6 characters (GX + 4, mkCode); it is sent as fmtCode shows
        /// it, GX-XXXX (protocol tableJoinCode). Null when it is not 6 characters (codeShort).
        /// </summary>
        public static string NormalizeCode(string input)
        {
            string raw = Regex.Replace((input ?? "").Trim().ToUpperInvariant(), "[^A-Z0-9]", "");
            return raw.Length == 6 ? raw.Substring(0, 2) + "-" + raw.Substring(2) : null;
        }

        private void SubmitCode(Dialog d)
        {
            var code = NormalizeCode(d.Code);
            if (code == null) { d.CodeError = S("codeShort"); d.Shake = Time.realtimeSinceStartup; RenderDialog(); return; }
            _ = JoinAsync(new TableJoinMessage { Code = code }, null);
        }

        /// <summary>d.pinErr (min-height 20, 800 14px, centred) / d.codeErr (min-height 18, 800 13px), #FF8A94.</summary>
        private void ErrorLine(VisualElement card, Dialog d)
        {
            bool code = d.Kind == "code";
            string text = code ? d.CodeError : d.Error ? S("wrongPass") : null;
            var line = card.Q<Label>(code ? "lobby.d.codeErr" : "lobby.d.pinErr");
            if (line == null)
            {
                line = Text("", code ? 13 : 14, "Baloo2-800", "#FF8A94", 0, code ? "lobby.d.codeErr" : "lobby.d.pinErr");
                line.style.marginTop = code ? 14 : 12; line.style.alignSelf = code ? Align.Stretch : Align.Center;
                // Before the buttons: the line is built once, the buttons after it.
                var buttons = card.Q(code ? "lobby-dialog-buttons" : "lobby-dialog-buttons");
                if (buttons != null) card.Insert(card.IndexOf(buttons), line); else card.Add(line);
            }
            line.text = text ?? "";
            line.style.height = string.IsNullOrEmpty(text) ? (code ? 18 : 20) : MenuView.CssLineHeight("Baloo2-800", code ? 13 : 14);
        }

        /// <summary>d.isErr: 450 wide, padding 30 30 24, gap 10 — red badge, Masa doldu / kapandı, the reason, Tamam / Benzer masa bul (İzle).</summary>
        private void BuildError(VisualElement card, Dialog d)
        {
            bool full = d.Kind == "full";
            Pad(card, 30, 30, 24, 30); ContentWidth(card, 450);
            var badge = Box(); Square(badge, 68); Raise(badge, "linear-gradient(#FF7A86,#E8455A)", 22, 5, "#9E1F33"); badge.Add(view.Icon(full ? IconFull : IconClosed, full ? 32 : 30, "#fff")); card.Add(badge);
            AddErrorTexts(card, full ? S("fullT") : S("closedT"), full ? S("fullB") : S("closedB"));
            // canWatch: a table that filled while you connected but plays with spectators on offers İzle instead.
            bool watch = full && d.Table != null && d.Table.Status == "playing" && d.Table.Spectators;
            card.Add(Pair(S("ok"), CloseDialog, watch ? S("watchIt") : S("similar"), () => { if (watch) { var t = d.Table; CloseDialog(); Watch(t); } else Similar(d.Table); }, "lobby.closeDlg#4", "lobby.d.alt", true, 10 + 10));
        }

        private void AddErrorTexts(VisualElement card, string title, string body)
        {
            var t = Text(title, 24, "Montserrat-700", "#F4F1E8", 1.2f, "lobby.d.title#2"); t.style.marginTop = 10 + 4; card.Add(t);
            var b = Text(body, 15, "Baloo2-700", "var(--mt)", 0, "lobby.d.body#3");
            Paragraph(b, 1.45f); b.style.maxWidth = 360; b.style.unityTextAlign = TextAnchor.MiddleCenter; b.style.marginTop = 10; card.Add(b);
        }

        /// <summary>Unity's own "no server" dialog in the error dialog's shape (the prototype has no offline lobby, EK-806).</summary>
        private void BuildOffline(VisualElement card)
        {
            Pad(card, 30, 30, 24, 30); ContentWidth(card, 450);
            var badge = Box(); Square(badge, 68); Raise(badge, "linear-gradient(#FF7A86,#E8455A)", 22, 5, "#9E1F33"); badge.Add(view.Icon("table-wifi-off", 32, "#fff")); card.Add(badge);
            AddErrorTexts(card, L("Sunucuya bağlanılamadı", "Could not reach the server"), L("Lobi için çevrimiçi olmalısın. Bağlantını kontrol edip yeniden dene.", "The lobby needs you online. Check your connection and try again."));
            card.Add(Pair(S("back"), () => { CloseDialog(); Back(); }, L("Tekrar dene", "Try again"), async () => { CloseDialog(); if (await Connect()) Render(); }, "lobby.closeDlg#4", "lobby.d.alt", true, 10 + 10));
        }

        /// <summary>An incoming invite (inviteIn) in the error dialog's shape: who, the stake and code; Sonra / Katıl (EK-806).</summary>
        private void BuildInvited(VisualElement card, Dialog d)
        {
            if (d.Incoming != null) { BuildIncoming(card, d.Incoming); return; }
            var m = d.Invite; Pad(card, 30, 30, 24, 30); ContentWidth(card, 450);
            var face = Box(); Square(face, 68); view.Decorate(face, m.From?.Color ?? "#3D7BFF", 34); Border(face, 3, new Color(1, 1, 1, .85f)); face.Add(Text(Initials(m.From?.Name), 24, "Montserrat-700", "#fff")); card.Add(face);
            AddErrorTexts(card, L((m.From?.Name ?? "Bir arkadaşın") + " seni masaya çağırıyor", (m.From?.Name ?? "A friend") + " invites you to a table"),
                L("Bahis " + view.N(m.Stake) + " · kod " + m.RoomCode, "Bet " + view.N(m.Stake) + " · code " + m.RoomCode));
            card.Add(Pair(L("Sonra", "Later"), CloseDialog, S("join"), () =>
            {
                if (m.Stake > backend.Chips) { CloseDialog(); Low(m.Stake, backend.Chips); return; }
                var code = m.RoomCode; CloseDialog(); _ = JoinAsync(new TableJoinMessage { Code = code }, null);
            }, "lobby.closeDlg#4", "lobby.d.alt", true, 10 + 10));
        }

        /// <summary>A v4 invite: who, the table (passBody: name · bet); Sonra declines, Katıl accepts (inviteRespond) and the room follows.</summary>
        private void BuildIncoming(VisualElement card, IncomingInvite m)
        {
            Pad(card, 30, 30, 24, 30); ContentWidth(card, 450);
            var face = Box(); Square(face, 68); view.Decorate(face, m.From?.Color ?? "#3D7BFF", 34); Border(face, 3, new Color(1, 1, 1, .85f)); face.Add(Text(Initials(m.From?.Name), 24, "Montserrat-700", "#fff")); card.Add(face);
            AddErrorTexts(card, L((m.From?.Name ?? "Bir arkadaşın") + " seni masaya çağırıyor", (m.From?.Name ?? "A friend") + " invites you to a table"),
                S("passBody", m.Table?.Name ?? "", view.N(m.Table?.Stake ?? 0)));
            card.Add(Pair(L("Sonra", "Later"), () => { CloseDialog(); _ = backend.RespondInviteAsync(m, false); }, S("join"), () =>
            {
                long stake = m.Table?.Stake ?? 0;
                if (stake > backend.Chips) { CloseDialog(); Low(stake, backend.Chips); return; }
                CloseDialog(); _ = JoinAsync(null, m.Table, false, () => backend.RespondInviteAsync(m, true));
            }, "lobby.closeDlg#4", "lobby.d.alt", true, 10 + 10));
        }

        /// <summary>d.isShare: 520 wide, padding 30 30 24, gap 12 — tick, Özel masan hazır, the invite code (+ PIN), Kodu kopyala / Paylaş, Bekleme odasına geç.</summary>
        private void BuildShare(VisualElement card, Dialog d)
        {
            var room = backend.Room; string code = room?.Code ?? "", pin = d.SharePin, name = room?.Table?.Name ?? "";
            Pad(card, 30, 30, 24, 30); ContentWidth(card, 520);
            // 64 circle #3CCB6E, 0 5px 0 #22914A, 0 0 30px rgba(60,203,110,.5).
            var ok = Box(); Square(ok, 64); Raise(ok, "#3CCB6E", 32, 5, "#22914A"); GalaxyFilters.Shadow(ok, 0, 0, 15, new Color(.235f, .8f, .43f, .5f)); ok.Add(view.Icon(IconCheck, 30, "#fff")); card.Add(ok);
            var title = Text(S("shareT"), 24, "Montserrat-700", "#F4F1E8", 1.2f, "lobby.text.shareT"); title.style.marginTop = 12; card.Add(title);
            var sub = Text(S("shareS"), 14, "Baloo2-700", "var(--mt)", 0, "lobby.text.shareS");
            Paragraph(sub, 1.45f); sub.style.maxWidth = 400; sub.style.unityTextAlign = TextAnchor.MiddleCenter; sub.style.marginTop = 12; card.Add(sub);
            // Code box: full width, gap 6, padding 18 0 16, margin-top 4, radius 20, 2px dashed --bd, rgba(0,0,0,.18).
            var box = Box(); box.style.alignSelf = Align.Stretch; box.style.alignItems = Align.Center; box.style.marginTop = 12 + 4; Pad(box, 18, 0, 16, 0); view.Decorate(box, "rgba(0,0,0,.18)", 20); DashedBorder(box, 2, view.C("var(--bd)"));
            var cap = Text(S("inviteCode"), 11, "Baloo2-800", "var(--mt)", 0, "lobby.text.inviteCode"); MenuView.Tracking(cap, 1.8f, 11); box.Add(cap);
            // 800 44px/1.1 --fL, letter-spacing 8px, gradient --lg1 → --a1 → --a2 clipped to the text (UF-801: --a1).
            var big = Text(code, 44, "Montserrat-800", "var(--a1)", 1.1f, "lobby.d.code"); MenuView.Tracking(big, 8, 44); big.style.marginTop = 6; box.Add(big);
            if (!string.IsNullOrEmpty(pin))
            {
                var pass = Row(); pass.style.marginTop = 6; Pad(pass, 5, 12, 5, 12); view.Decorate(pass, "var(--dp)", 10);
                pass.Add(view.Icon(IconLock, 13, "var(--a1)")); var pl = Text(S("passLbl"), 13, "Baloo2-800", "var(--ic)"); pl.style.marginLeft = 8; pass.Add(pl);
                var pv = Text(pin, 15, "Montserrat-700", "#F4F1E8", 0, "lobby.d.pass"); MenuView.Tracking(pv, 4, 15); pv.style.marginLeft = 8; pass.Add(pv); box.Add(pass);
            }
            card.Add(box);
            // shareText(n, c, p): Galaxy 101'de "n" masasına gel! Kod: c[ · Şifre: p] (the PIN part only with a PIN).
            string text = L("Galaxy 101'de \"" + name + "\" masasına gel! Kod: " + code + (string.IsNullOrEmpty(pin) ? "" : " · Şifre: " + pin),
                "Join my \"" + name + "\" table on Galaxy 101! Code: " + code + (string.IsNullOrEmpty(pin) ? "" : " · Password: " + pin));
            var buttons = Row(); buttons.style.alignSelf = Align.Stretch; buttons.style.marginTop = 12;
            // d.copy: 700 16px, padding 13, radius 16, copied → #3CCB6E / #062B14 / 0 5px 0 #22914A.
            var copy = IconButton("lobby.d.copy", IconCopy, d.Copied ? S("copied") : S("copy"), d.Copied ? "#3CCB6E" : "var(--pnH)", d.Copied ? "#062B14" : "#F4F1E8", d.Copied ? "#22914A" : "var(--pnS)");
            Tap(copy, () => { view.Copy(code); d.Copied = true; RenderDialog(); }); buttons.Add(copy);
            // d.share: navigator.share where there is one, otherwise the text is copied and the button says so.
            var share = IconButton("lobby.d.share", IconShare, d.Shared ? S("shared") : S("share"), "#3D7BFF", "#fff", "#2148B0"); share.style.marginLeft = 10;
            Tap(share, () => { view.Copy(text); d.Shared = true; RenderDialog(); }); buttons.Add(share);
            card.Add(buttons);
            var toRoom = Text(d.First ? S("toRoom") : S("back2"), 17, "Montserrat-800", "var(--aT)", 0, "lobby.d.toRoom"); toRoom.style.alignSelf = Align.Stretch; toRoom.style.marginTop = 12 + 4; Pad(toRoom, 15, 0, 15, 0); toRoom.style.unityTextAlign = TextAnchor.MiddleCenter;
            Raise(toRoom, "linear-gradient(var(--a1),var(--a2))", 16, 5, "var(--aS)"); Tap(toRoom, CloseDialog); card.Add(toRoom);
        }

        /// <summary>A share-dialog button: icon 17 + label (gap 8), padding 13, radius 16, 0 5px 0 shade.</summary>
        private VisualElement IconButton(string name, string icon, string text, string bg, string fg, string shade)
        {
            var button = Row(); button.name = name; button.style.flexGrow = 1; button.style.flexBasis = 0; button.style.justifyContent = Justify.Center; Pad(button, 13, 13, 13, 13);
            Raise(button, bg, 16, 5, shade);
            var i = view.Icon(icon, 17, fg); i.style.marginRight = 8; button.Add(i); button.Add(Text(text, 16, "Montserrat-700", fg));
            return button;
        }

        /// <summary>
        /// Two equal buttons (grid 1fr 1fr, gap 10): the first --pnH (700 16px), the second gold (800 16px, or --pnH / --mt
        /// when <paramref name="secondOn"/> is false); padding 14, radius 16, 0 5px 0 ledge.
        /// </summary>
        private VisualElement Pair(string first, Action firstGo, string second, Action secondGo, string firstName, string secondName, bool secondOn, float marginTop)
        {
            var row = Row(); row.name = "lobby-dialog-buttons"; row.style.alignSelf = Align.Stretch; row.style.marginTop = marginTop;
            var a = Text(first, 16, "Montserrat-700", "#F4F1E8", 0, firstName); a.style.flexGrow = 1; a.style.flexBasis = 0; Pad(a, 14, 0, 14, 0); a.style.unityTextAlign = TextAnchor.MiddleCenter; Raise(a, "var(--pnH)", 16, 5, "var(--pnS)"); Tap(a, firstGo); row.Add(a);
            var b = Text(second, 16, "Montserrat-800", secondOn ? "var(--aT)" : "var(--mt)", 0, secondName); b.style.flexGrow = 1; b.style.flexBasis = 0; b.style.marginLeft = 10; Pad(b, 14, 0, 14, 0); b.style.unityTextAlign = TextAnchor.MiddleCenter;
            Raise(b, secondOn ? "linear-gradient(var(--a1),var(--a2))" : "var(--pnH)", 16, 5, secondOn ? "var(--aS)" : "var(--pnS)"); Tap(b, secondGo); row.Add(b);
            return row;
        }

        /// <summary>lbShake .4s both: 0 → −8 → 8 → −8 → 8 → 0 px at 20 % steps (linear between them).</summary>
        private void Shake(VisualElement element, float at)
        {
            if (at <= 0) return;
            Animate(() =>
            {
                float t = (Time.realtimeSinceStartup - at) / .4f;
                float x = t <= 0 || t >= 1 ? 0 : t < .2f ? -8 * t / .2f : t < .8f ? (Mathf.FloorToInt((t - .2f) / .2f) % 2 == 0 ? Mathf.Lerp(-8, 8, (t - .2f) % .2f / .2f) : Mathf.Lerp(8, -8, (t - .2f) % .2f / .2f)) : Mathf.Lerp(8, 0, (t - .8f) / .2f);
                element.style.translate = new Translate(x, 0);
            }, element);
        }

        /// <summary>A lbSpin ring: a <paramref name="width"/> px border in <paramref name="color"/> with a transparent top, one turn per .8 s.</summary>
        private VisualElement Spinner(int size, string color, float width = 3)
        {
            var ring = Box(); Square(ring, size); Radius(ring, size / 2f); Border(ring, width, view.C(color)); ring.style.borderTopColor = Color.clear;
            Animate(() => ring.style.rotate = new Rotate(Mathf.Repeat(Time.realtimeSinceStartup / .8f, 1) * 360), ring); return ring;
        }

        /// <summary>
        /// lbModal: from translateY(16px) scale(.96) and transparent. Dialogs ease with the default curve; Masa aç and the
        /// share dialog with cubic-bezier(.2,1.2,.4,1) (a slight overshoot), the invite panel with cubic-bezier(.22,1,.36,1).
        /// </summary>
        private void Pop(VisualElement modal, float began, float seconds, bool overshoot, bool settle = false)
        {
            if (Time.realtimeSinceStartup - began >= seconds) return;
            modal.style.opacity = 0;
            Animate(() =>
            {
                float u = Mathf.Clamp01((Time.realtimeSinceStartup - began) / seconds); if (u >= 1 && modal.style.opacity.value >= 1) return;
                float k = overshoot ? MenuView.CssBezier(.2f, 1.2f, .4f, 1, u) : settle ? MenuView.CssBezier(.22f, 1, .36f, 1, u) : MenuView.CssBezier(.25f, .1f, .25f, 1, u);
                modal.style.opacity = Mathf.Clamp01(k); modal.style.translate = new Translate(0, 16 * (1 - k)); modal.style.scale = new Scale(Vector2.one * Mathf.LerpUnclamped(.96f, 1, k));
            }, modal);
        }
    }
}
