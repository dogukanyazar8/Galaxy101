using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Galaxy101
{
    // The menu services: the server's data and actions while signed in (OnlineMenu), the design's sample services
    // otherwise. A signed-in action answers at once ("sent"); its outcome refreshes the menu and shows as a notice.

    public sealed class OnlineProfileService : IProfileService
    {
        private readonly IProfileService sample; private readonly OnlineMenu online;
        public OnlineProfileService(IProfileService sample, OnlineMenu online) { this.sample = sample; this.online = online; }
        public JObject Get() => sample.Get();
        public bool Save(string name, string color, string status, int frame) => sample.Save(name, color, status, frame);
        public Task<string> SaveAsync(string name, string color, string status, int frame) => sample.SaveAsync(name, color, status, frame);
        public JObject GetStats() => !online.Active ? sample.GetStats() : (JObject)(online.Stats ?? Empty()).DeepClone();
        public JArray GetBadges() => online.Active ? (JArray)online.Badges.DeepClone() : sample.GetBadges();
        public JArray GetHistory() => online.Active ? (JArray)online.History.DeepClone() : sample.GetHistory();
        public bool Logout() => sample.Logout();
        public bool RequestDelete() => sample.RequestDelete();
        public Task<string> DeleteAccountAsync() => sample.DeleteAccountAsync();
        private static JObject Empty() => new JObject
        {
            ["hands"] = 0, ["wins"] = 0, ["winRate"] = 0, ["bestOpening"] = 0, ["okeyFinish"] = 0, ["level"] = 1, ["xp"] = 0, ["nextXp"] = 375,
            ["lp"] = 0, ["nextLp"] = 100, ["league"] = "b3", ["nextLeague"] = "b2", ["modeWinRates"] = new JArray(0, 0, 0, 0),
        };
    }

    public sealed class OnlineEconomyService : IEconomyService
    {
        private readonly IEconomyService sample; private readonly OnlineMenu online;
        public OnlineEconomyService(IEconomyService sample, OnlineMenu online) { this.sample = sample; this.online = online; }
        public JObject Balance => sample.Balance; // the menu model mirrors the server wallet
        public JArray GetOffers(int tab)
        {
            var offers = sample.GetOffers(tab); var catalog = online.Active ? online.Catalog : null;
            if (catalog == null) return offers;
            // The server's amounts and prices over the panel's art and names (the catalog ids are the panel's).
            var server = tab == 0 ? catalog.Chips : tab == 1 ? catalog.Gems : catalog.Skins;
            foreach (JObject offer in offers)
            {
                var match = server.Find(o => o.Id == (string)offer["id"]); if (match == null) continue;
                if (match.Kind == "skin") offer["price"] = match.GemPrice;
                else { offer["amount"] = match.Kind == "chips" ? match.Chips : match.Gems; offer["bonus"] = match.BonusPercent; offer["price"] = match.ReferencePrice; }
                offer["pop"] = match.Popular;
            }
            return offers;
        }
        public JObject GetDeal()
        {
            var deal = sample.GetDeal(); var server = online.Active ? online.Catalog?.Deal : null;
            if (server != null) { deal["chips"] = server.Chips; deal["gems"] = server.Gems; deal["price"] = server.ReferencePrice; if (server.ReferenceOldPrice is decimal old) deal["oldPrice"] = old; }
            return deal;
        }
        public bool Purchase(string id) { if (!online.Active) return sample.Purchase(id); _ = online.PurchaseAsync(id); return true; }
        public Task<string> PurchaseAsync(string id) => online.Active ? online.PurchaseAsync(id) : sample.PurchaseAsync(id);
    }

    public sealed class OnlineFriendsService : IFriendsService
    {
        private readonly IFriendsService sample; private readonly OnlineMenu online; private readonly MenuModel model;
        public OnlineFriendsService(IFriendsService sample, OnlineMenu online, MenuModel model) { this.sample = sample; this.online = online; this.model = model; }
        public JArray GetFriends(bool onlineOnly = false, string query = "")
        {
            if (!online.Active) return sample.GetFriends(onlineOnly, query);
            var result = new JArray();
            foreach (JObject f in online.Friends)
                if ((!onlineOnly || (bool?)f["on"] == true) && Matches(f, query)) { var row = (JObject)f.DeepClone(); row["invited"] = model.Invited.Contains((string)f["id"]); result.Add(row); }
            return result;
        }
        public JArray GetRequests(string query = "")
        {
            if (!online.Active) return sample.GetRequests(query);
            var result = new JArray(); foreach (JObject r in online.Requests) if (Matches(r, query)) result.Add(r.DeepClone());
            return result;
        }
        private static bool Matches(JObject row, string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return true;
            var turkish = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
            string text = query.Trim().TrimStart('#');
            return ((string)row["name"] ?? "").ToLower(turkish).Contains(text.ToLower(turkish)) ||
                MockFriendsService.NormalizeCode((string)row["code"]).Replace("-", "").Contains(MockFriendsService.NormalizeCode(text).Replace("-", ""));
        }
        /// <summary>Signed in, an invitation needs a waiting room: it is sent from the lobby's table (the friend joins its code).</summary>
        public bool Invite(string id)
        {
            if (!online.Active) return sample.Invite(id);
            online.Tell("Davet etmek için lobide bir masa aç", "Open a table in the lobby to invite friends", "info");
            return false;
        }
        public bool Accept(string id) { if (!online.Active) return sample.Accept(id); _ = online.AcceptAsync(id); return true; }
        public bool Decline(string id) { if (!online.Active) return sample.Decline(id); _ = online.DeclineAsync(id); return true; }
        public JObject Search(string query) => online.Active ? null : sample.Search(query);
        public Task<JObject> SearchAsync(string query) => online.Active ? online.SearchAsync(query) : sample.SearchAsync(query);
        public bool SendRequest(string code) { if (!online.Active) return sample.SendRequest(code); _ = online.SendRequestAsync(code); return true; }
    }

    public sealed class OnlineLeaderboardService : ILeaderboardService
    {
        private readonly ILeaderboardService sample; private readonly OnlineMenu online;
        public OnlineLeaderboardService(ILeaderboardService sample, OnlineMenu online) { this.sample = sample; this.online = online; }
        public JObject Get(string scope)
        {
            if (!online.Active) return sample.Get(scope);
            if (scope != "weekly" && scope != "season" && scope != "friends") throw new ArgumentException("Unknown leaderboard scope.", nameof(scope));
            return (JObject)online.Board(scope).DeepClone();
        }
    }

    public sealed class OnlineRewardsService : IRewardsService
    {
        private readonly IRewardsService sample; private readonly OnlineMenu online;
        public OnlineRewardsService(IRewardsService sample, OnlineMenu online) { this.sample = sample; this.online = online; }
        public DateTime ResetTime => online.Active ? online.ResetTime : sample.ResetTime;
        public JArray GetStreak() => online.Active ? (JArray)online.Streak.DeepClone() : sample.GetStreak();
        public bool ClaimDay() { if (!online.Active) return sample.ClaimDay(); if (!online.CanClaimDay) return false; _ = online.ClaimDayAsync(); return true; }
        public JArray GetQuests() => online.Active ? (JArray)online.Quests.DeepClone() : sample.GetQuests();
        public bool ClaimQuest(int index) { if (!online.Active) return sample.ClaimQuest(index); _ = online.ClaimQuestAsync(index); return true; }
    }

    public sealed class OnlineSettingsService : ISettingsService
    {
        private readonly ISettingsService sample; private readonly OnlineMenu online;
        public OnlineSettingsService(ISettingsService sample, OnlineMenu online) { this.sample = sample; this.online = online; }
        public JObject Load() => sample.Load();
        public JObject ResetToDefaults() => sample.ResetToDefaults();
        // Saved on this device first (it applies at once), then on the account.
        public bool Save(JObject draft) => Saved(sample.Save(draft));
        public bool SetLanguage(string language) => Saved(sample.SetLanguage(language));
        public bool SetPlanet(string planet) { if (!sample.SetPlanet(planet)) return false; online.SavePlanet(planet); return true; }
        public bool SetDisplay(string mode, string resolution) => sample.SetDisplay(mode, resolution);
        public bool SetFrameRate(int choice) => sample.SetFrameRate(choice);
        private bool Saved(bool saved) { if (saved) online.SaveSettings(); return saved; }
    }

    public sealed class OnlineShareService : IShareService
    {
        private readonly IShareService sample; private readonly OnlineMenu online; private readonly MenuModel model;
        public OnlineShareService(IShareService sample, OnlineMenu online, MenuModel model) { this.sample = sample; this.online = online; this.model = model; }
        public string ProfileUrl => !online.Active ? sample.ProfileUrl : online.ShareUrl ?? "https://galaxy101.app/p/" + model.FriendCode;
        public string ShareTo(string platform)
        {
            if (!online.Active) return sample.ShareTo(platform);
            sample.ShareTo(platform); // validates the target
            CopyToClipboard(); return ProfileUrl;
        }
        public void CopyToClipboard(string text = null) => UnityEngine.GUIUtility.systemCopyBuffer = text ?? ProfileUrl;
    }
}
