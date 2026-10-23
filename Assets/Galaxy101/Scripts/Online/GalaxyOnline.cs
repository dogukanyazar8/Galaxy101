using System;
using System.Threading.Tasks;
using Galaxy101.Contracts;
using Newtonsoft.Json;
using UnityEngine;

namespace Galaxy101.Online
{
    /// <summary>
    /// The signed-in session for the whole run (survives scene loads): the HTTP API, the WebSocket, the player's
    /// profile and wallet as the server last reported them, and "Beni hatırla" (spec/18): a registered account's refresh
    /// token is kept in PlayerPrefs; a guest session is never kept (misafir ilerlemesi kaydedilmez).
    /// </summary>
    public sealed class GalaxyOnline : MonoBehaviour
    {
        public const string SessionKey = "galaxy101.session";

        private sealed class Saved { public string RefreshToken; public string AccountId; public bool Guest; }

        public static GalaxyOnline Instance { get; private set; }
        public GalaxyApi Api { get; private set; }
        public GalaxyRealtime Realtime { get; private set; }
        public ProfileResponse Profile { get; private set; }
        public WalletResponse Wallet { get; private set; }
        public bool Guest { get; private set; }
        public bool Remember { get; private set; }
        public bool SignedIn => Api != null && Api.SignedIn;
        /// <summary>Profile or wallet changed (server push or refresh).</summary>
        public event Action Changed;

        public static GalaxyOnline Ensure(string server = null)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Galaxy101 online"); DontDestroyOnLoad(go);
            Instance = go.AddComponent<GalaxyOnline>();
            Instance.Api = new GalaxyApi(server);
            Instance.Realtime = new GalaxyRealtime(Instance.Api);
            Instance.Api.TokensChanged += Instance.Persist;
            Instance.Realtime.On<WalletResponse>(MessageTypes.Wallet, w => { Instance.Wallet = w; Instance.Changed?.Invoke(); });
            return Instance;
        }

        private void Update() => Realtime?.Pump();

        private void OnDestroy() { Realtime?.Dispose(); if (Instance == this) Instance = null; }

        /// <summary>Signs back in with a remembered account. False when there is none, or it expired.</summary>
        public async Task<bool> ResumeAsync()
        {
            var saved = Load();
            if (saved == null || string.IsNullOrEmpty(saved.RefreshToken)) return false;
            Remember = true; Guest = saved.Guest;
            if (!await Api.ResumeAsync(saved.RefreshToken)) { Forget(); return false; }
            await AfterSignInAsync();
            return true;
        }

        public async Task GuestAsync(string name, string color) { Guest = true; Remember = false; await Api.GuestAsync(name, color); await AfterSignInAsync(); }
        public async Task LoginAsync(string email, string password, bool remember) { Guest = false; Remember = remember; await Api.LoginAsync(email, password); await AfterSignInAsync(); }
        public async Task RegisterAsync(RegisterRequest request) { Guest = false; Remember = true; await Api.RegisterAsync(request); await AfterSignInAsync(); }

        public async Task SignOutAsync()
        {
            Realtime.Dispose();
            await Api.LogoutAsync();
            Profile = null; Wallet = null; Forget();
            Changed?.Invoke();
        }

        public async Task RefreshAsync()
        {
            Profile = await Api.ProfileAsync();
            Wallet = await Api.WalletAsync();
            Changed?.Invoke();
        }

        private async Task AfterSignInAsync()
        {
            Persist(Api.Tokens);
            await RefreshAsync();
            try { await Realtime.ConnectAsync(); }
            catch (Exception e) { Debug.LogWarning("Realtime connection failed: " + e.Message); }
        }

        private void Persist(TokenResponse tokens)
        {
            if (tokens == null || Guest || !Remember) return;
            try { PlayerPrefs.SetString(SessionKey, JsonConvert.SerializeObject(new Saved { RefreshToken = tokens.RefreshToken, AccountId = tokens.AccountId.ToString(), Guest = Guest })); PlayerPrefs.Save(); }
            catch (Exception e) { Debug.LogWarning("Could not remember the session: " + e.Message); }
        }

        private static Saved Load()
        {
            try { var text = PlayerPrefs.GetString(SessionKey, ""); return string.IsNullOrEmpty(text) ? null : JsonConvert.DeserializeObject<Saved>(text); }
            catch (Exception) { return null; }
        }

        public static void Forget() { try { PlayerPrefs.DeleteKey(SessionKey); PlayerPrefs.Save(); } catch (Exception) { } }
    }
}
