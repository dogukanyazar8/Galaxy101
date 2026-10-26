using System;
using System.Threading;
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
        /// <summary>Bumped at every sign-in and sign-out: an answer that started under another number belongs to another
        /// account (or none) and must not land in this one's screens.</summary>
        public int Generation { get; private set; }
        /// <summary>Profile or wallet changed (server push or refresh).</summary>
        public event Action Changed;
        /// <summary>The socket came back after a drop: the server forgot waiting rooms and subscriptions, so listeners
        /// subscribe and resync again.</summary>
        public event Action Reconnected;
        private bool reconnecting;
        private CancellationTokenSource reconnectLife;
        private int apiGeneration;
        private int refreshRevision;
        internal SemaphoreSlim ProfileSaving { get; } = new SemaphoreSlim(1, 1);
        /// <summary>Another device owns this session. Only an explicit sign-in may take it back.</summary>
        public bool Replaced { get; private set; }
        /// <summary>A v4 table reconnects by itself (protocol.json reconnect: 5 × 3 s, then "Lobiye dön"); the session's backoff waits meanwhile.</summary>
        public bool SuspendReconnect { get; set; }

        public static GalaxyOnline Ensure(string server = null)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Galaxy101 online"); DontDestroyOnLoad(go);
            Instance = go.AddComponent<GalaxyOnline>();
            Instance.Api = new GalaxyApi(server);
            Instance.Realtime = new GalaxyRealtime(Instance.Api);
            var online = Instance;
            online.Api.TokensChanged += online.OnTokensChanged;
            online.Realtime.On<WalletResponse>(MessageTypes.Wallet, w => { if (online != null && online.SignedIn && !online.Replaced) online.Apply(w); });
            Instance.Realtime.Closed += Instance.OnRealtimeClosed;
            V4TableSession.Watch(Instance);   // a v4 matchStart opens the table (waiting room, invite, ranked, vsBotsStart)
            return Instance;
        }

        /// <summary>A dropped socket reconnects with backoff (1, 2, 4, 8, then every 15 s) while signed in; a session
        /// taken over by another device stays closed.</summary>
        private async void OnRealtimeClosed(string reason)
        {
            if (reason == GalaxyRealtime.ReplacedReason)
            {
                Replaced = true; reconnectLife?.Cancel(); Forget(); Changed?.Invoke();
                return;
            }
            // A v4 table reconnects by itself (protocol.json reconnect); two loops would open two sockets.
            if (Replaced || !SignedIn || reconnecting || SuspendReconnect || V4TableSession.Current is { Closed: false }) return;
            reconnecting = true;
            int generation = Generation;
            var owner = new CancellationTokenSource(); reconnectLife = owner;
            try
            {
                for (int attempt = 0; Owns(generation) && !Replaced && !Realtime.Connected; attempt++)
                {
                    await Task.Delay(Math.Min(1000 << Math.Min(attempt, 4), 15000), owner.Token);
                    if (!Owns(generation) || Replaced || Realtime.Connected || SuspendReconnect || V4TableSession.Current is { Closed: false }) break;
                    try { await Realtime.ConnectAsync(); }
                    catch (Exception e) { Debug.LogWarning("Realtime reconnect failed: " + e.Message); }
                }
                if (Owns(generation) && !Replaced && Realtime.Connected) Reconnected?.Invoke();
            }
            catch (OperationCanceledException) when (owner.IsCancellationRequested) { }
            finally { if (reconnectLife == owner) { reconnecting = false; reconnectLife = null; } owner.Dispose(); }
        }

        private void Update() => Realtime?.Pump();

        /// <summary>Start the backoff reconnect now (a v4 table that gave up hands the socket back to the session).</summary>
        public void ReconnectNow() { if (!Replaced && SignedIn && !Realtime.Connected) OnRealtimeClosed("table"); }

        private void OnDestroy() { reconnectLife?.Cancel(); if (Api != null) Api.TokensChanged -= OnTokensChanged; Realtime?.Dispose(); if (Instance == this) Instance = null; }

        public bool Owns(int generation) => this != null && Instance == this && SignedIn && Generation == generation;

        private void OnTokensChanged(TokenResponse tokens)
        {
            if (apiGeneration != Api.Generation)
            {
                apiGeneration = Api.Generation; Generation++;
                reconnectLife?.Cancel(); reconnectLife = null; reconnecting = false;
                Realtime.ResetSession(); Replaced = false;
                SuspendReconnect = false;
                Profile = null; Wallet = null;
                V4TableSession.Current?.Dispose();
                Forget();
            }
            Persist(tokens);
            Changed?.Invoke();
        }

        /// <summary>Signs back in with a remembered account. False when there is none, or it expired.</summary>
        public async Task<bool> ResumeAsync()
        {
            var saved = Load();
            if (saved == null || string.IsNullOrEmpty(saved.RefreshToken)) return false;
            Remember = true; Guest = saved.Guest;
            var resume = Api.ResumeAsync(saved.RefreshToken);
            int generation = Generation;
            bool restored;
            try { restored = await resume; }
            catch (ApiException e) when (e.Offline || e.Code == ErrorCodes.Banned)
            {
                // A network failure or temporary ban must not erase the remembered account.
                if (this != null && Generation == generation)
                    try { PlayerPrefs.SetString(SessionKey, JsonConvert.SerializeObject(saved)); PlayerPrefs.Save(); }
                    catch (Exception failure) { Debug.LogWarning("Could not remember the session: " + failure.Message); }
                throw;
            }
            if (!restored) { if (Generation == generation) Forget(); return false; }
            if (!Owns(generation)) return false;
            await AfterSignInAsync(generation);
            if (!Owns(generation)) return false;
            return true;
        }

        public async Task GuestAsync(string name, string color) { Guest = true; Remember = true; var signIn = Api.GuestAsync(name, color); int generation = Generation; await signIn; await AfterSignInAsync(generation); }
        public async Task LoginAsync(string email, string password, bool remember) { Guest = false; Remember = remember; var signIn = Api.LoginAsync(email, password); int generation = Generation; await signIn; await AfterSignInAsync(generation); }
        public async Task RegisterAsync(RegisterRequest request) { Guest = false; Remember = true; var signIn = Api.RegisterAsync(request); int generation = Generation; await signIn; await AfterSignInAsync(generation); }

        public async Task SignOutAsync()
        {
            await Api.LogoutAsync();
        }

        /// <summary>A balance the server returned with another answer (a claim, a purchase).</summary>
        public void Apply(WalletResponse wallet)
        {
            if (wallet == null || !SignedIn || Replaced) return;
            Wallet = wallet; Changed?.Invoke();
        }

        public async Task RefreshAsync()
        {
            int generation = Generation, revision = ++refreshRevision;
            if (!Owns(generation) || Replaced) return;
            try
            {
                var profile = await Api.ProfileAsync();
                if (!Owns(generation) || Replaced || revision != refreshRevision) return;
                var wallet = await Api.WalletAsync();
                if (!Owns(generation) || Replaced || revision != refreshRevision) return;
                Profile = profile; Wallet = wallet; Changed?.Invoke();
            }
            catch (ApiException) when (!Owns(generation)) { }
        }

        private async Task AfterSignInAsync(int generation)
        {
            if (!Owns(generation)) return;
            Persist(Api.Tokens);
            await RefreshAsync();
            if (!Owns(generation) || Replaced) return;
            try { await Realtime.ConnectAsync(); }
            catch (Exception e) { if (Owns(generation) && !Replaced) { Debug.LogWarning("Realtime connection failed: " + e.Message); OnRealtimeClosed("connect failed"); } } // keep trying
        }

        private void Persist(TokenResponse tokens)
        {
            if (tokens == null || !Remember || Replaced) return;
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
