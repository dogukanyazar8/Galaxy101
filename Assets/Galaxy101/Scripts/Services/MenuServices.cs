using System;
using Newtonsoft.Json.Linq;

namespace Galaxy101
{
    public interface IProfileService
    {
        JObject Get(); bool Save(string name, string color, string status, int frame);
        JObject GetStats(); JArray GetBadges(); JArray GetHistory(); bool Logout(); bool RequestDelete();
    }
    public interface IEconomyService
    {
        JObject Balance { get; } JArray GetOffers(int tab); JObject GetDeal(); bool Purchase(string id);
    }
    public interface IFriendsService
    {
        JArray GetFriends(bool onlineOnly = false, string query = ""); JArray GetRequests(string query = "");
        bool Invite(string id); bool Accept(string id); bool Decline(string id); JObject Search(string query); bool SendRequest(string code);
    }
    public interface ILeaderboardService { JObject Get(string scope); }
    public interface IRewardsService
    {
        JArray GetStreak(); bool ClaimDay(); JArray GetQuests(); bool ClaimQuest(int index); DateTime ResetTime { get; }
    }
    public interface ISettingsService
    {
        JObject Load(); bool Save(JObject draft); JObject ResetToDefaults(); bool SetLanguage(string language); bool SetPlanet(string planet); bool SetResolution(int height); bool SetFrameRate(int choice);
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
        public MenuServices(MenuModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            var resource = UnityEngine.Resources.Load<UnityEngine.TextAsset>("Galaxy101/supplemental");
            if (resource == null) throw new InvalidOperationException("Galaxy101 supplemental menu content is missing.");
            var extra = JObject.Parse(resource.text);
            Profile = new MockProfileService(model, extra);
            Economy = new MockEconomyService(model, extra);
            Friends = new MockFriendsService(model);
            Leaderboard = new MockLeaderboardService(model);
            Rewards = new MockRewardsService(model);
            Settings = new MockSettingsService(model);
            Share = new MockShareService();
        }
    }
}
