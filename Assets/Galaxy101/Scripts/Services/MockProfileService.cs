using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Galaxy101
{
    public sealed class MockProfileService : IProfileService
    {
        private readonly MenuModel model;
        private readonly JObject content;
        public bool LoggedOut { get; private set; }
        public bool DeletionRequested { get; private set; }
        public MockProfileService(MenuModel model, JObject content) { this.model = model; this.content = content; }
        public JObject Get() => (JObject)model.Profile.DeepClone();
        public bool Save(string name, string color, string status, int frame)
        {
            var value = new JObject { ["name"] = name, ["color"] = color, ["status"] = status ?? "", ["frame"] = frame };
            if (!ProfileValidation.IsValid(value)) return false;
            var previous = model.Profile;
            model.Profile = ProfileValidation.Sanitize(value);
            if (!model.Persist()) { model.Profile = previous; return false; }
            model.NotifyChanged();
            return true;
        }

        /// <summary>
        /// Signed in, the server account holds the profile (other players see it): it is saved there first, and only an
        /// accepted change is kept on this device — a refused or unsent one is not shown as saved.
        /// </summary>
        public async Task<string> SaveAsync(string name, string color, string status, int frame)
        {
            var value = new JObject { ["name"] = name, ["color"] = color, ["status"] = status ?? "", ["frame"] = frame };
            if (!ProfileValidation.IsValid(value)) return "validation";
            int mine = ++saves;
            var online = Online.GalaxyOnline.Instance;
            if (online != null && online.SignedIn)
            {
                int generation = online.Generation;
                // Keep the server's PUT order as well as the local revision order. An older PUT cannot finish after the newer one.
                await online.ProfileSaving.WaitAsync();
                try
                {
                    if (mine != saves || !online.Owns(generation)) return null;
                    try { await online.Api.PutAsync<JObject>("/v1/profile", new Contracts.ProfileUpdateRequest { Name = name.Trim(), Color = color, Status = status ?? "", Frame = frame }); }
                    catch (Online.ApiException e) { return e.Offline ? "offline" : e.Code; }
                    if (mine != saves || !online.Owns(generation)) return null;
                    try { await online.RefreshAsync(); } catch (Online.ApiException) { } // saved; the next refresh brings the card
                    if (mine != saves || !online.Owns(generation)) return null;
                }
                finally { online.ProfileSaving.Release(); }
            }
            return Save(name, color, status, frame) ? null : "local";
        }
        private int saves;
        /// <summary>The sample stats; v4 adds the month's win-rate change of the design's sample (G101-Menu mp.stats: "Bu ay +%3").</summary>
        public JObject GetStats() { var stats = (JObject)content["stats"].DeepClone(); if (stats["winRateMonth"] == null) stats["winRateMonth"] = 3; return stats; }
        public JArray GetBadges() => (JArray)content["badges"].DeepClone();
        public JArray GetHistory() => (JArray)content["history"].DeepClone();
        // Signed in to the game server, the two account actions are real requests, not local flags: "çıkış" ends the
        // server session and forgets the remembered token; deletion asks the server to delete the account, then signs out.
        public bool Logout()
        {
            var online = Online.GalaxyOnline.Instance;
            bool signedIn = online != null && online.SignedIn;
            // Once per sign-in: a later sign-in in the same menu can log out again; a second tap while signing out cannot.
            if (signedIn ? loggedOutAt == online.Generation : LoggedOut) return false;
            LoggedOut = true;
            if (signedIn) { loggedOutAt = online.Generation; _ = SignOutAsync(online); }
            model.NotifyChanged(); return true;
        }
        private int loggedOutAt = -1;
        /// <summary>The design's demo flow (no server account): only a local flag.</summary>
        public bool RequestDelete()
        {
            if (DeletionRequested) return false; DeletionRequested = true;
            model.NotifyChanged(); return true;
        }

        public async Task<string> DeleteAccountAsync()
        {
            var online = Online.GalaxyOnline.Instance;
            if (online == null || !online.SignedIn) return RequestDelete() ? null : "duplicate";
            int generation = online.Generation;
            try { await online.Api.SendAsync<JObject>("DELETE", "/v1/account"); }
            catch (Online.ApiException e) { UnityEngine.Debug.LogWarning("Account deletion failed: " + e.Code); return e.Code; }
            if (!online.Owns(generation)) return "offline";
            DeletionRequested = true;
            await SignOutAsync(online);
            model.NotifyChanged(); return null;
        }

        private static async Task SignOutAsync(Online.GalaxyOnline online)
        {
            try { await online.SignOutAsync(); }
            catch (System.Exception e) { UnityEngine.Debug.LogWarning("Sign-out failed: " + e.Message); }
        }

    }
}
