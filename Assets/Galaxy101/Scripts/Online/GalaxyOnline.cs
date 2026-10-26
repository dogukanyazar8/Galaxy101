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
    /// token is kept in PlayerPrefs when the player asks; a guest's always is — the guest screen promises "İlerlemen bu
    /// cihazda saklanır" and a later account link, and a guest has no other way back to their account.
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
        /// <summary>The socket came back after a drop: the server forgot waiting rooms and subscriptions, so listeners
        /// subscribe and resync again.</summary>
        public event Action Reconnected;
        private bool reconnecting;
        /// <summary>A v4 table reconnects by itself (protocol.json reconnect: 5 × 3 s, then "Lobiye dön"); the session's backoff waits meanwhile.</summary>
        public bool SuspendReconnect { get; set; }

        public static GalaxyOnline Ensure(string server = null)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Galaxy101 online"); DontDestroyOnLoad(go);
            Instance = go.AddComponent<GalaxyOnline>();
            Instance.Api = new GalaxyApi(server);
            Instance.Realtime = new GalaxyRealtime(Instance.Api);
            Instance.Api.TokensChanged += Instance.Persist;
            Instance.Realtime.On<WalletResponse>(MessageTypes.Wallet, w => { Instance.Wallet = w; Instance.Changed?.Invoke(); });
            Instance.Realtime.Closed += Instance.OnRealtimeClosed;
            V4TableSession.Watch(Instance);   // a v4 matchStart opens the table (waiting room, invite, ranked, vsBotsStart)
            return Instance;
        }

        /// <summary>A dropped socket reconnects with backoff (1, 2, 4, 8, then every 15 s) while signed in; a session
        /// taken over by another device stays closed.</summary>
        private async void OnRealtimeClosed(string reason)
        {
            // A v4 table reconnects by itself (protocol.json reconnect); two loops would open two sockets.
            if (reason == GalaxyRealtime.ReplacedReason || reconnecting || SuspendReconnect || V4TableSession.Current is { Closed: false }) return;
            reconnecting = true;
            try
            {
                for (int attempt = 0; this != null && SignedIn && !Realtime.Connected; attempt++)
                {
                    await Task.Delay(Math.Min(1000 << Math.Min(attempt, 4), 15000));
                    if (this == null || !SignedIn || Realtime.Connected) break;
                    try { await Realtime.ConnectAsync(); }
                    catch (Exception e) { Debug.LogWarning("Realtime reconnect failed: " + e.Message); }
                }
                if (this != null && Realtime.Connected) Reconnected?.Invoke();
            }
            finally { reconnecting = false; }
        }

        private void Update() => Realtime?.Pump();

        /// <summary>Start the backoff reconnect now (a v4 table that gave up hands the socket back to the session).</summary>
        public void ReconnectNow() { if (!Realtime.Connected) OnRealtimeClosed("table"); }

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

        public async Task GuestAsync(string name, string color) { Guest = true; Remember = true; await Api.GuestAsync(name, color); await AfterSignInAsync(); }
        public async Task LoginAsync(string email, string password, bool remember) { Guest = false; Remember = remember; await Api.LoginAsync(email, password); await AfterSignInAsync(); }
        public async Task RegisterAsync(RegisterRequest request) { Guest = false; Remember = true; await Api.RegisterAsync(request); await AfterSignInAsync(); }

        public async Task SignOutAsync()
        {
            Realtime.Dispose();
            await Api.LogoutAsync();
            Profile = null; Wallet = null; Forget();
            Changed?.Invoke();
        }

        /// <summary>A balance the server returned with another answer (a claim, a purchase).</summary>
        public void Apply(WalletResponse wallet)
        {
            if (wallet == null) return;
            Wallet = wallet; Changed?.Invoke();
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
            catch (Exception e) { Debug.LogWarning("Realtime connection failed: " + e.Message); OnRealtimeClosed("connect failed"); } // keep trying
        }

        private void Persist(TokenResponse tokens)
        {
            if (tokens == null || !Remember) return;
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
