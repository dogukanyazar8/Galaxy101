using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Galaxy101.Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using UnityEngine;
using UnityEngine.Networking;

namespace Galaxy101.Online
{
    /// <summary>A failed server call: the stable <see cref="Code"/> (ErrorCodes / TableErrors) and the HTTP status.</summary>
    public sealed class ApiException : Exception
    {
        public string Code { get; }
        public long Status { get; }
        public JObject Body { get; }
        public ApiException(string code, long status, JObject body = null) : base(code + " (" + status + ")") { Code = code; Status = status; Body = body; }
        /// <summary>No answer at all (server down, no network).</summary>
        public bool Offline => Status == 0;
    }

    /// <summary>
    /// The game server's HTTP side (Server/README): JSON in camelCase, bearer access token, one transparent refresh on
    /// 401. The base address comes from <c>-galaxyServer &lt;url&gt;</c>, the GALAXY101_SERVER environment variable or
    /// PlayerPrefs "galaxy101.server"; local development defaults to http://localhost:5173.
    /// </summary>
    public sealed class GalaxyApi
    {
        public const string DefaultServer = "http://localhost:5173";
        public static readonly JsonSerializerSettings Json = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(), NullValueHandling = NullValueHandling.Ignore,
        };

        public string BaseUrl { get; }
        public TokenResponse Tokens { get; private set; }
        public bool SignedIn => Tokens != null;
        public int TimeoutSeconds = 10;
        /// <summary>The refresh token rotated (persist it if the player chose "Beni hatırla").</summary>
        public event Action<TokenResponse> TokensChanged;

        public GalaxyApi(string baseUrl = null) { BaseUrl = (baseUrl ?? ResolveServer()).TrimEnd('/'); }

        /// <summary>
        /// Set by the development state router: state runs never reach a real account or server (the prototype's
        /// G101State.sandbox), so a remembered session cannot skip the sign-in states or replace the state account.
        /// </summary>
        public static string Override;

        public static string ResolveServer()
        {
            if (!string.IsNullOrEmpty(Override)) return Override;
            var args = Environment.GetCommandLineArgs();
            int i = Array.IndexOf(args, "-galaxyServer");
            if (i >= 0 && i + 1 < args.Length) return args[i + 1];
            var env = Environment.GetEnvironmentVariable("GALAXY101_SERVER");
            if (!string.IsNullOrEmpty(env)) return env;
            try { var saved = PlayerPrefs.GetString("galaxy101.server", ""); if (!string.IsNullOrEmpty(saved)) return saved; } catch (Exception) { }
            return DefaultServer;
        }

        public void Use(TokenResponse tokens) { Tokens = tokens; TokensChanged?.Invoke(tokens); }
        public void SignOut() { Tokens = null; TokensChanged?.Invoke(null); }

        // ---------- auth (spec/18) ----------
        public async Task<TokenResponse> GuestAsync(string name, string color)
        { var t = await SendAsync<TokenResponse>("POST", "/v1/auth/guest", new GuestLoginRequest { Name = name, Color = color, Platform = Application.platform.ToString() }, false); Use(t); return t; }
        public async Task<TokenResponse> RegisterAsync(RegisterRequest request)
        { request.Platform = Application.platform.ToString(); var t = await SendAsync<TokenResponse>("POST", "/v1/auth/register", request, false); Use(t); return t; }
        public async Task<TokenResponse> LoginAsync(string email, string password)
        { var t = await SendAsync<TokenResponse>("POST", "/v1/auth/login", new PasswordLoginRequest { Email = email, Password = password }, false); Use(t); return t; }
        public async Task<bool> ResumeAsync(string refreshToken)
        {
            try { Use(await SendAsync<TokenResponse>("POST", "/v1/auth/refresh", new RefreshRequest { RefreshToken = refreshToken }, false)); return true; }
            // A ban is not a dead session: the token stays remembered for after it, and the caller shows why.
            catch (ApiException e) when (!e.Offline && e.Code != ErrorCodes.Banned) { return false; }
        }
        public async Task LogoutAsync()
        {
            if (Tokens != null) try { await SendAsync<JObject>("POST", "/v1/auth/logout", new RefreshRequest { RefreshToken = Tokens.RefreshToken }); } catch (ApiException) { }
            SignOut();
        }

        // ---------- menu data ----------
        public Task<ProfileResponse> ProfileAsync() => SendAsync<ProfileResponse>("GET", "/v1/profile");
        public Task<WalletResponse> WalletAsync() => SendAsync<WalletResponse>("GET", "/v1/wallet");
        public Task<RealtimeTicketResponse> TicketAsync() => SendAsync<RealtimeTicketResponse>("POST", "/v1/realtime/ticket", null);
        public Task<List<FriendResponse>> RecentPlayersAsync() => SendAsync<List<FriendResponse>>("GET", "/v1/players/recent");
        public Task<JObject> InviteAsync(Guid friend, string code) => SendAsync<JObject>("POST", "/v1/friends/" + friend + "/invite", new InviteRequest { RoomCode = code });
        public Task<T> GetAsync<T>(string path) => SendAsync<T>("GET", path);
        public Task<T> PostAsync<T>(string path, object body) => SendAsync<T>("POST", path, body);
        public Task<T> PutAsync<T>(string path, object body) => SendAsync<T>("PUT", path, body);

        public async Task<T> SendAsync<T>(string method, string path, object body = null, bool auth = true)
        {
            if (!ServerAddress.IsAllowed(BaseUrl, out var refused))
            {
                Debug.LogError("Galaxy 101 server address refused (" + BaseUrl + "): " + refused);
                throw new ApiException("insecure_server", 0);
            }
            for (int attempt = 0; ; attempt++)
            {
                using (var request = new UnityWebRequest(BaseUrl + path, method))
                {
                    if (body != null || method != "GET")
                    {
                        request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body == null ? "{}" : JsonConvert.SerializeObject(body, Json)));
                        request.SetRequestHeader("Content-Type", "application/json");
                    }
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.timeout = TimeoutSeconds;
                    var sent = auth ? Tokens : null;
                    if (sent != null) request.SetRequestHeader("Authorization", "Bearer " + sent.AccessToken);
                    try { await Await(request.SendWebRequest()); }
                    catch (Exception e) when (!(e is ApiException))
                    {
                        // UnityWebRequest refuses some requests synchronously (bad URL, blocked transport): an offline answer.
                        Debug.LogWarning("Galaxy 101 request " + method + " " + path + " failed: " + e.Message);
                        throw new ApiException("offline", 0);
                    }
                    long status = request.responseCode;
                    string text = request.downloadHandler.text;
                    if (request.result == UnityWebRequest.Result.ConnectionError || status == 0) throw new ApiException("offline", 0);
                    if (status == 401 && sent != null && attempt == 0 && await RefreshAfter(sent)) continue;
                    if (status >= 200 && status < 300)
                    {
                        if (typeof(T) == typeof(JObject) && string.IsNullOrWhiteSpace(text)) return default;
                        return string.IsNullOrWhiteSpace(text) ? default : JsonConvert.DeserializeObject<T>(text, Json);
                    }
                    JObject error = null;
                    try { error = string.IsNullOrWhiteSpace(text) ? null : JObject.Parse(text); } catch (JsonException) { }
                    throw new ApiException((string)error?["code"] ?? "http_" + status, status, error);
                }
            }
        }

        // Parallel requests that meet an expired access token share one refresh: the server revokes the whole token family
        // when a refresh token is presented twice. A request sent with tokens already rotated just retries with the new ones.
        private Task<bool> refreshing;
        private Task<bool> RefreshAfter(TokenResponse sent)
        {
            if (Tokens != sent) return Task.FromResult(Tokens != null);
            return refreshing ?? (refreshing = RefreshAsync(sent.RefreshToken));
        }
        private async Task<bool> RefreshAsync(string refreshToken)
        {
            try { return await ResumeAsync(refreshToken); }
            finally { refreshing = null; }
        }

        private static Task Await(UnityWebRequestAsyncOperation operation)
        {
            var done = new TaskCompletionSource<bool>();
            if (operation.isDone) done.SetResult(true); else operation.completed += _ => done.TrySetResult(true);
            return done.Task;
        }
    }
}
