using Newtonsoft.Json.Linq;
using UnityEngine.UIElements;

namespace Galaxy101
{
    public sealed class FriendsController
    {
        private string query = "";

        public string[] Tabs(MenuView v) => new[]
        {
            v.L("Çevrimiçi", "Online") + " · " + v.Services.Friends.GetFriends(true).Count,
            v.L("Tümü", "All") + " · " + v.Services.Friends.GetFriends().Count,
            v.L("İstekler", "Requests") + " · " + v.Services.Friends.GetRequests().Count
        };

        public VisualElement Build(MenuView v)
        {
            var root = PanelUi.Shell(v, "Friends");
            var toolbar = root.Q<VisualElement>("toolbar");
            toolbar.style.marginBottom = 14;
            // Reference search: height 48, padding 0 6px 0 16px, gap 10, 18px search icon, 700 15px body input.
            var input = PanelUi.Input(v, "friend-query", query, v.L("Oyuncu adı veya kod ara", "Search by name or code")); input.AddToClassList("bare");
            var searchIcon = v.Icon("search", 18, "mt"); searchIcon.style.marginRight = 10;
            var search = v.Row(searchIcon, input);
            PanelUi.Grow(search); search.style.minWidth = 0; search.style.height = 48; search.style.paddingLeft = 16; search.style.paddingRight = 6;
            var clear = PanelUi.CustomButton(v, "", () => input.value = "", "");
            clear.style.paddingLeft = clear.style.paddingRight = clear.style.paddingTop = clear.style.paddingBottom = 0;
            clear.style.width = 32; clear.style.minWidth = 32; clear.style.height = 32; clear.style.borderTopLeftRadius = clear.style.borderTopRightRadius = clear.style.borderBottomLeftRadius = clear.style.borderBottomRightRadius = 10;
            clear.Add(v.Icon("close", 14, "mt"));
            clear.RegisterCallback<PointerEnterEvent>(_ => clear.style.backgroundColor = v.C("pnH")); clear.RegisterCallback<PointerLeaveEvent>(_ => clear.style.backgroundColor = UnityEngine.Color.clear);
            clear.style.display = query.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            search.Add(clear);
            v.Decorate(search, "dp", 14);
            toolbar.Add(search);
            // Reference code pill: height 48, padding 0 16px, sd fill, 1px dashed bd, gap 10, 16px copy glyph.
            var code = PanelUi.CodeStrip(v, 12, 17, 14); v.Decorate(code, "sd", 14); code.style.height = 48; code.style.paddingLeft = code.style.paddingRight = 16; PanelUi.Gap(code, 10);
            var copy = PanelUi.CustomButton(v, "", () => v.Copy(v.Model.FriendCode), "");
            copy.style.width = copy.style.height = 16; copy.style.minWidth = 16; copy.style.paddingLeft = copy.style.paddingRight = copy.style.paddingTop = copy.style.paddingBottom = 0;
            copy.Add(v.Icon("copy", 16)); code.Add(copy);
            toolbar.Add(code);
            var add = PanelUi.CssButton(v, v.L("Arkadaş ekle", "Add friend"), () => v.OpenDialog("add"), "primary", 0, 20, 16);
            add.style.flexDirection = FlexDirection.Row;
            var addIcon = v.Icon("add", 18, "aT"); addIcon.style.marginRight = 8; add.Insert(0, addIcon);
            add.style.height = 48; toolbar.Add(add); PanelUi.Gap(toolbar, 12);
            var list = v.Scroll(); list.name = "friend-list";
            root.Q<VisualElement>("list").Add(list);
            void Fill()
            {
                list.Clear();
                var tab = v.Tab("friends");
                var rows = tab == 2 ? v.Services.Friends.GetRequests(query) : v.Services.Friends.GetFriends(tab == 0, query);
                foreach (JObject person in rows) list.Add(BuildRow(v, person, tab == 2));
                if (rows.Count == 0)
                {
                    var text = query.Trim().Length > 0 ? v.L("\"" + query.Trim() + "\" ile eşleşen arkadaş yok", "No friends match \"" + query.Trim() + "\"") : tab == 2 ? v.L("Bekleyen istek yok", "No pending requests") : v.L("Burada henüz arkadaş yok", "No friends here yet");
                    var empty = v.Text(text, 15, false, "mt");
                    empty.style.paddingTop = 40; empty.style.unityTextAlign = UnityEngine.TextAnchor.MiddleCenter; list.Add(empty);
                }
            }
            input.RegisterValueChangedCallback(evt => { query = evt.newValue; clear.style.display = query.Length > 0 ? DisplayStyle.Flex : DisplayStyle.None; Fill(); });
            Fill();
            return root;
        }

        private static VisualElement BuildRow(MenuView v, JObject p, bool request)
        {
            var row = v.Row(); row.AddToClassList("g-friend-row");
            var avatar = v.Box(); avatar.style.width = avatar.style.height = 48; avatar.style.flexShrink = 0;
            var face = PanelUi.Person(v, p, 48); face.Q<Label>().style.fontSize = 17; avatar.Add(face);
            var online = (bool?)p["on"] ?? false;
            if (!online && !request) face.style.opacity = .55f;
            var dot = PanelUi.Circle(v, 16, online ? "#3CCB6E" : "#5C6680");
            dot.style.position = Position.Absolute; dot.style.right = -2; dot.style.bottom = -2;
            dot.style.borderTopWidth = dot.style.borderBottomWidth = dot.style.borderLeftWidth = dot.style.borderRightWidth = 3;
            dot.style.borderTopColor = dot.style.borderBottomColor = dot.style.borderLeftColor = dot.style.borderRightColor = v.C("sd");
            avatar.Add(dot); avatar.style.marginRight = 14; row.Add(avatar);
            var name = v.Row(v.Text((string)p["name"], 17, true), PanelUi.LevelChip(v, v.L("SV ", "LV ") + (int)p["lv"]));
            PanelUi.Gap(name, 8);
            var status = p["st"] as JArray;
            var statusLabel = v.Text(status == null ? "" : v.L((string)status[0], (string)status[1]), 13, false, online ? (string)p["sc"] ?? "mt" : "mt"); statusLabel.style.marginTop = 2;
            var details = v.Column(name, statusLabel);
            PanelUi.Grow(details); row.Add(details);
            var id = (string)p["id"];
            if (request)
            {
                // Row actions: padding 10px 18px, radius 12, 0 3px 0 depth, 700 14px, gap 8.
                row.Add(PanelUi.CssButton(v, v.L("Kabul et", "Accept"), () => v.Services.Friends.Accept(id), "success", 10, 18, 14, 12, 3));
                var decline = PanelUi.CssButton(v, v.L("Reddet", "Decline"), () => v.Services.Friends.Decline(id), "secondary", 10, 18, 14, 12, 3);
                decline.style.marginLeft = 8; row.Add(decline);
            }
            else if ((string)p["act"] == "invite")
            {
                var sent = v.Model.Invited.Contains(id);
                var invite = PanelUi.CssButton(v, sent ? v.L("Gönderildi", "Sent") : v.L("Davet et", "Invite"), () => { if (!v.Model.Invited.Contains(id)) v.Services.Friends.Invite(id); }, sent ? "secondary" : "primary", 10, 18, 14, 12, 3);
                invite.name = "invite-" + id; row.Add(invite);
            }
            else
            {
                var watch = (string)p["act"] == "watch";
                row.Add(PanelUi.CssButton(v, watch ? v.L("İzle", "Watch") : v.L("Mesaj", "Message"), () => v.Toast(watch ? v.L("İzleme yakında", "Watching is coming soon") : v.L("Çevrimdışı", "Offline")), "secondary", 10, 18, 14, 12, 3));
            }
            return row;
        }
    }
}
