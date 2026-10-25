using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace Galaxy101
{
    public sealed class AddFriendController
    {
        private string query = "";
        private JObject result;
        private bool searched;

        public void OnOpen(MenuView v) { query = ""; result = null; searched = false; }

        public VisualElement Build(MenuView v)
        {
            var copyLabel = v.Text(v.L("Kopyala", "Copy"), 14, true);
            var root = PanelUi.Shell(v, "AddFriend");
            PanelUi.Header(v, root, v.L("Arkadaş ekle", "Add friend"), v.L("Arkadaş kodu veya oyuncu adıyla ara", "Search with a friend code or player name"), "add", "linear-gradient(160deg,#7AA6FF,#3D6BF0)", 22);
            root.Q<VisualElement>("footer").style.display = DisplayStyle.None;
            var body = root.Q<VisualElement>("content");
            var field = PanelUi.Input(v, "add-friend-query", query, v.L("Örn. MK-3321", "e.g. MK-3321"), -1, 16, true); field.AddToClassList("bare"); MenuView.Tracking(field, 1, 16);
            var resultBox = v.Row(); resultBox.name = "friend-search-result"; resultBox.style.height = 96; resultBox.style.marginTop = 14; resultBox.style.marginBottom = 14; PanelUi.Pad(resultBox, 0, 16); v.Decorate(resultBox, "sd", 18);
            void Fill()
            {
                resultBox.Clear();
                var friend = (bool?)result?["isFriend"] == true;
                field.EnableInClassList("invalid", false); SetRing(field.panel?.focusController?.focusedElement == field);
                if (!searched || result == null || friend)
                {
                    var message = !searched ? v.L("Kod ya da ad girip Ara'ya bas.", "Enter a code or name and press Search.") : friend ? v.L("Bu oyuncuyla zaten arkadaşsınız.", "You are already friends with this player.") : v.L("Oyuncu bulunamadı. Kodu kontrol edip tekrar dene.", "No player found. Check the code and try again.");
                    var label = PanelUi.Wrap(v, message, 14, searched ? "#FF8A94" : "mt"); PanelUi.Grow(label); label.style.unityTextAlign = UnityEngine.TextAnchor.MiddleCenter; resultBox.Add(label); return;
                }
                var avatar = PanelUi.Person(v, result, 52); avatar.Q<Label>().style.fontSize = 18; avatar.style.marginRight = 14; resultBox.Add(avatar);
                var name = v.Row(v.Text((string)result["name"], 18, true), PanelUi.LevelChip(v, v.L("SV ", "LV ") + (int)result["lv"])); PanelUi.Gap(name, 8);
                var details = v.Column(name, v.Text("#" + (string)result["code"], 13, false, "mt")); PanelUi.Grow(details); resultBox.Add(details);
                var sent = v.Model.SentRequests.Contains((string)result["code"]);
                var send = PanelUi.CssButton(v, sent ? v.L("İstek gönderildi", "Request sent") : v.L("İstek gönder", "Send request"), () =>
                {
                    if (!v.Model.SentRequests.Contains((string)result["code"]) && v.Services.Friends.SendRequest((string)result["code"])) Fill();
                }, sent ? "secondary" : "primary", 11, 18, 15, 12, 3);
                send.name = "send-friend-request"; if (sent) send.style.color = v.C("#8EF0B0"); resultBox.Add(send);
            }
            // Signed in, the server's directory answers; a newer query or a closed dialog drops an older answer.
            async void Search()
            {
                if (string.IsNullOrWhiteSpace(query)) return;
                var asked = query; var found = await v.Services.Friends.SearchAsync(asked);
                if (asked != query || v.Modals.Dialog != "add") return;
                result = found; searched = true;
                if (resultBox.panel != null) Fill(); else v.Refresh();
            }
            // Reference: 50px query box (padding 0 14px, gap 10, "#" in 700 16px mt) and a padding-sized "Ara" button (0 22px, 700 16px).
            var prefix = v.Text("#", 16, true, "mt"); prefix.style.marginRight = 10;
            var queryBox = v.Row(prefix, field); v.Decorate(queryBox, "dp", 14); PanelUi.Grow(queryBox); queryBox.style.height = 50; queryBox.style.paddingLeft = queryBox.style.paddingRight = 14;
            var search = PanelUi.CssButton(v, v.L("Ara", "Search"), Search, "primary", 0, 22, 16); search.name = "search-add-friend"; search.style.height = 50;
            field.RegisterCallback<FocusInEvent>(_ => SetRing(true)); field.RegisterCallback<FocusOutEvent>(_ => SetRing(false));
            void SetRing(bool focused)
            {
                bool invalid = searched && result == null; float width = invalid || focused ? 2 : 0;
                queryBox.style.borderLeftWidth = queryBox.style.borderRightWidth = queryBox.style.borderTopWidth = queryBox.style.borderBottomWidth = width;
                queryBox.style.borderLeftColor = queryBox.style.borderRightColor = queryBox.style.borderTopColor = queryBox.style.borderBottomColor = v.C(invalid ? "#FF5E6C" : "a1");
                queryBox.style.paddingLeft = queryBox.style.paddingRight = 14 - width;
            }
            var searchRow = v.Row(queryBox, search); PanelUi.Gap(searchRow, 10); body.Add(searchRow); body.Add(resultBox);
            field.RegisterValueChangedCallback(evt => { query = evt.newValue; searched = false; result = null; Fill(); });
            field.RegisterCallback<KeyDownEvent>(evt => { if (evt.keyCode == UnityEngine.KeyCode.Return || evt.keyCode == UnityEngine.KeyCode.KeypadEnter) { Search(); evt.StopImmediatePropagation(); } });
            // Reference: dashed strip, padding 12px 14px, radius 16, gap 12; "Kopyala" with a 14px copy glyph (padding 9px 14px, radius 12, depth 3).
            var code = PanelUi.CodeStrip(v, 13, 18, 16); PanelUi.Pad(code, 12, 14); PanelUi.Gap(code, 12); code.Add(PanelUi.Spacer(v));
            Button copy = null;
            copy = PanelUi.CssButton(v, "", () =>
            {
                v.Copy(v.Model.FriendCode); copyLabel.text = v.L("Kopyalandı", "Copied");
                copy.schedule.Execute(() => copyLabel.text = v.L("Kopyala", "Copy")).StartingIn(1500);
            }, "secondary", 9, 14, 14, 12, 3);
            copy.style.flexDirection = FlexDirection.Row; var copyIcon = v.Icon("copy", 14, "#F4F1E8"); copyIcon.style.marginRight = 7; copy.Add(copyIcon); copy.Add(copyLabel);
            code.Add(copy); body.Add(code); Fill(); return root;
        }
    }
}
