using System;
using Newtonsoft.Json.Linq;

namespace Galaxy101
{
    public interface IProfileService
    {
        JObject Get(); bool Save(string name, string color, string status, int frame);
        JObject GetStats(); JArray GetBadges(); JArray GetHistory(); bool Logout(); bool RequestDelete();
        /// <summary>Signed in: the server first, then this device; null on success, else the error code (nothing changed).</summary>
        System.Threading.Tasks.Task<string> SaveAsync(string name, string color, string status, int frame);
        /// <summary>Signed in: deletes the server account now (irreversible) and signs out; null on success, else the error code.</summary>
        System.Threading.Tasks.Task<string> DeleteAccountAsync();
    }
    public interface IEconomyService
    {
        JObject Balance { get; } JArray GetOffers(int tab); JObject GetDeal(); bool Purchase(string id);
        /// <summary>null when bought, else the error code (signed in: the server's answer).</summary>
        System.Threading.Tasks.Task<string> PurchaseAsync(string id);
    }
    public interface IFriendsService
    {
        JArray GetFriends(bool onlineOnly = false, string query = ""); JArray GetRequests(string query = "");
        bool Invite(string id); bool Accept(string id); bool Decline(string id); JObject Search(string query); bool SendRequest(string code);
        /// <summary>The player a code or name finds, or null (signed in: the server's directory).</summary>
        System.Threading.Tasks.Task<JObject> SearchAsync(string query);
    }
    public interface ILeaderboardService { JObject Get(string scope); }
    public interface IRewardsService
    {
        JArray GetStreak(); bool ClaimDay(); JArray GetQuests(); bool ClaimQuest(int index); DateTime ResetTime { get; }
    }
    public interface ISettingsService
    {
        JObject Load(); bool Save(JObject draft); JObject ResetToDefaults(); bool SetLanguage(string language); bool SetPlanet(string planet); bool SetDisplay(string mode, string resolution); bool SetFrameRate(int choice);
    }
    public interface IShareService
    {
        string ProfileUrl { get; } string ShareTo(string platform); void CopyToClipboard(string text = null);
    }

    public sealed class MenuServices
    {
        public IProfileService Profile { get; }
        public IEconomyService Economy { get; }
        public IFriendsService Friends { get; }
        public ILeaderboardService Leaderboard { get; }
        public IRewardsService Rewards { get; }
        public ISettingsService Settings { get; }
        public IShareService Share { get; }
        /// <summary>The server's side of the menu while signed in (data per panel, action notices).</summary>
        public OnlineMenu Online { get; }
        public MenuServices(MenuModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            var resource = UnityEngine.Resources.Load<UnityEngine.TextAsset>("Galaxy101/supplemental");
            if (resource == null) throw new InvalidOperationException("Galaxy101 supplemental menu content is missing.");
            var extra = JObject.Parse(resource.text);
            Online = new OnlineMenu(model, extra);
            Profile = new OnlineProfileService(new MockProfileService(model, extra), Online);
            Economy = new OnlineEconomyService(new MockEconomyService(model, extra), Online);
            Friends = new OnlineFriendsService(new MockFriendsService(model), Online, model);
            Leaderboard = new OnlineLeaderboardService(new MockLeaderboardService(model), Online);
            Rewards = new OnlineRewardsService(new MockRewardsService(model), Online);
            Settings = new OnlineSettingsService(new MockSettingsService(model), Online);
            Share = new OnlineShareService(new MockShareService(), Online, model);
        }
    }
}
