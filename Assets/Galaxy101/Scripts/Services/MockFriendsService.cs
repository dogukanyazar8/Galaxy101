using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Galaxy101
{
    public sealed class MockFriendsService : IFriendsService
    {
        private readonly MenuModel model;
        private readonly List<JObject> accepted = new List<JObject>();
        private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
        public MockFriendsService(MenuModel model) { this.model = model; }
        public static string NormalizeCode(string value)
        {
            var compact = CompactCode(value);
            return Regex.IsMatch(compact, @"\A[A-Z]{2}[0-9]{4}\z") ? compact.Substring(0, 2) + "-" + compact.Substring(2) : compact;
        }
        private static string CompactCode(string value) => string.Concat((value ?? "").Where(x => x != '#' && x != '-' && !char.IsWhiteSpace(x))).ToUpperInvariant();
        private IEnumerable<JObject> AllFriends() => ((JArray)model.Content["friends"]).OfType<JObject>().Concat(accepted);
        private string Code(JObject friend) => (string)friend["code"] ?? (string)model.Content["friendCodes"][(string)friend["id"]] ?? "";
        private bool Match(JObject row, string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            string name = query.Trim().TrimStart('#').ToLower(Turkish);
            string code = CompactCode(query);
            return ((string)row["name"] ?? "").ToLower(Turkish).Contains(name) ||
                (code.Length > 0 && CompactCode(Code(row)).Contains(code));
        }
        private JObject FriendCopy(JObject source)
        {
            var result = (JObject)source.DeepClone();
            result["code"] = Code(source);
            result["invited"] = model.Invited.Contains((string)source["id"]);
            return result;
        }
        public JArray GetFriends(bool onlineOnly = false, string query = "") => new JArray(AllFriends()
            .Where(x => (!onlineOnly || (bool?)x["on"] == true) && Match(x, query)).Select(FriendCopy));
        public JArray GetRequests(string query = "") => new JArray(((JArray)model.Content["friendRequests"]).OfType<JObject>()
            .Where(x => !model.RequestsHandled.Contains((string)x["id"]) && Match(x, query)).Select(FriendCopy));
        public bool Invite(string id)
        {
            if (string.IsNullOrEmpty(id) || !AllFriends().Any(x => (string)x["id"] == id && (bool?)x["on"] == true && (string)x["act"] == "invite") || !model.Invited.Add(id)) return false;
            model.NotifyChanged(); return true;
        }
        public bool Accept(string id) => Handle(id, true);
        public bool Decline(string id) => Handle(id, false);
        private bool Handle(string id, bool accept)
        {
            if (string.IsNullOrEmpty(id) || model.RequestsHandled.Contains(id)) return false;
            var request = ((JArray)model.Content["friendRequests"]).OfType<JObject>().FirstOrDefault(x => (string)x["id"] == id);
            if (request == null) return false;
            if (accept)
            {
                var friend = FriendCopy(request);
                friend["on"] = false; friend["act"] = "msg";
                friend["st"] = new JArray("Çevrimdışı", "Offline");
                accepted.Add(friend);
            }
            model.RequestsHandled.Add(id);
            model.NotifyChanged(); return true;
        }
        public System.Threading.Tasks.Task<JObject> SearchAsync(string query) => System.Threading.Tasks.Task.FromResult(Search(query));
        public JObject Search(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return null;
            string normalized = NormalizeCode(query);
            var existing = AllFriends().FirstOrDefault(x => NormalizeCode(Code(x)) == normalized);
            if (existing == null)
            {
                var directoryResult = ((JArray)model.Content["searchDirectory"]).OfType<JObject>().FirstOrDefault(x => NormalizeCode((string)x["code"]) == normalized);
                if (directoryResult != null) return SearchCopy(directoryResult, false);
                existing = AllFriends().FirstOrDefault(x => ((string)x["name"]).ToLower(Turkish).Contains(query.Trim().ToLower(Turkish)));
            }
            if (existing != null) return SearchCopy(FriendCopy(existing), true);
            var result = ((JArray)model.Content["searchDirectory"]).OfType<JObject>()
                .FirstOrDefault(x => ((string)x["name"]).ToLower(Turkish).Contains(query.Trim().ToLower(Turkish)));
            return result == null ? null : SearchCopy(result, AllFriends().Any(x => NormalizeCode(Code(x)) == NormalizeCode((string)result["code"])));
        }
        private JObject SearchCopy(JObject source, bool isFriend)
        {
            var result = (JObject)source.DeepClone();
            result["code"] = NormalizeCode((string)result["code"]);
            result["isFriend"] = isFriend;
            result["sent"] = model.SentRequests.Contains((string)result["code"]);
            return result;
        }
        public bool SendRequest(string code)
        {
            string normalized = NormalizeCode(code);
            if (AllFriends().Any(x => NormalizeCode(Code(x)) == normalized)) return false;
            if (!((JArray)model.Content["searchDirectory"]).OfType<JObject>().Any(x => NormalizeCode((string)x["code"]) == normalized) || !model.SentRequests.Add(normalized)) return false;
            model.NotifyChanged(); return true;
        }
    }
}
