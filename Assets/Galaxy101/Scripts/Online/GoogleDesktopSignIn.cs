using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Galaxy101.Contracts;
using UnityEngine;

namespace Galaxy101.Online
{
    /// <summary>The player closed Google's page, chose "Cancel", or did not finish in time.</summary>
    public sealed class GoogleSignInCancelledException : Exception
    {
        public bool TimedOut { get; }
        public GoogleSignInCancelledException(bool timedOut) : base(timedOut ? "Google sign-in timed out." : "Google sign-in was cancelled.") { TimedOut = timedOut; }
    }

    /// <summary>
    /// Google sign-in for the desktop game (OAuth 2.0 for native apps, RFC 8252): Google's consent page opens in the
    /// player's browser; the answer comes back to a one-shot listener on 127.0.0.1 (a random free port) with an
    /// authorization code bound to this attempt by <c>state</c> and a PKCE S256 challenge (RFC 7636). The code and its
    /// verifier go to the game server, which exchanges them with the client secret (<see cref="GalaxyApi.GoogleCodeAsync"/>).
    /// A raw socket rather than HttpListener: no URL reservation or administrator rights on Windows, works under IL2CPP.
    /// </summary>
    public static class GoogleDesktopSignIn
    {
        public const string AuthorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
        public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

        /// <summary>Platforms that can open a browser and listen on loopback (phones need the platform sign-in SDK).</summary>
        public static bool Supported => Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor
            || Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.OSXEditor
            || Application.platform == RuntimePlatform.LinuxPlayer || Application.platform == RuntimePlatform.LinuxEditor;

        /// <summary>Opens the browser (<paramref name="openUrl"/>, default <see cref="Application.OpenURL"/>) and waits for Google's code.</summary>
        public static async Task<GoogleCodeRequest> AuthorizeAsync(string clientId, bool english, CancellationToken cancel, Action<string> openUrl = null)
        {
            if (string.IsNullOrEmpty(clientId)) throw new ArgumentException("No Google client id.", nameof(clientId));
            string verifier = Base64Url(Random(32)), state = Base64Url(Random(16));
            string challenge;
            using (var sha = SHA256.Create()) challenge = Base64Url(sha.ComputeHash(Encoding.ASCII.GetBytes(verifier)));

            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start(1);
            try
            {
                int port = ((IPEndPoint)listener.LocalEndpoint).Port;
                string redirect = "http://127.0.0.1:" + port + "/";
                (openUrl ?? Application.OpenURL)(AuthorizationUrl(clientId, redirect, challenge, state, english));
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel))
                {
                    timeout.CancelAfter(Timeout);
                    // Stop() makes a pending accept throw: the wait ends with the token, never hangs.
                    using (timeout.Token.Register(listener.Stop))
                    {
                        while (true)
                        {
                            TcpClient client;
                            try { client = await listener.AcceptTcpClientAsync(); }
                            catch (Exception) when (timeout.IsCancellationRequested) { throw new GoogleSignInCancelledException(!cancel.IsCancellationRequested); }
                            using (client)
                            {
                                var query = await ReadQueryAsync(client, timeout.Token);
                                // The browser may ask for /favicon.ico or a stray page first: wait for Google's answer.
                                if (query == null || !query.TryGetValue("state", out var echoed)) { await RespondAsync(client, 404, null, english); continue; }
                                if (!FixedTimeEquals(echoed, state)) { await RespondAsync(client, 400, false, english); continue; }
                                if (query.TryGetValue("code", out var code) && !string.IsNullOrEmpty(code))
                                {
                                    await RespondAsync(client, 200, true, english);
                                    return new GoogleCodeRequest { Code = code, CodeVerifier = verifier, RedirectUri = redirect };
                                }
                                // error=access_denied: the player chose Cancel on Google's page.
                                await RespondAsync(client, 200, false, english);
                                throw new GoogleSignInCancelledException(false);
                            }
                        }
                    }
                }
            }
            finally { listener.Stop(); }
        }

        public static string AuthorizationUrl(string clientId, string redirect, string challenge, string state, bool english) =>
            AuthorizationEndpoint + "?response_type=code&scope=" + Uri.EscapeDataString("openid email profile") +
            "&client_id=" + Uri.EscapeDataString(clientId) + "&redirect_uri=" + Uri.EscapeDataString(redirect) +
            "&code_challenge=" + challenge + "&code_challenge_method=S256&state=" + state +
            "&prompt=select_account&hl=" + (english ? "en" : "tr");

        private static async Task<Dictionary<string, string>> ReadQueryAsync(TcpClient client, CancellationToken token)
        {
            client.ReceiveTimeout = 5000;
            var stream = client.GetStream();
            var buffer = new byte[8192]; int read = 0;
            // The request line and headers only (a GET has no body); 8 KB is far beyond Google's redirect.
            while (read < buffer.Length)
            {
                int n = await stream.ReadAsync(buffer, read, buffer.Length - read, token);
                if (n <= 0) break;
                read += n;
                if (Encoding.ASCII.GetString(buffer, 0, read).Contains("\r\n\r\n")) break;
            }
            string head = Encoding.ASCII.GetString(buffer, 0, read);
            int end = head.IndexOf("\r\n", StringComparison.Ordinal);
            var line = (end >= 0 ? head.Substring(0, end) : head).Split(' ');
            if (line.Length < 2 || line[0] != "GET" || !line[1].StartsWith("/?", StringComparison.Ordinal)) return null;
            var query = new Dictionary<string, string>();
            foreach (var pair in line[1].Substring(2).Split('&'))
            {
                int eq = pair.IndexOf('=');
                if (eq <= 0) continue;
                query[Uri.UnescapeDataString(pair.Substring(0, eq).Replace('+', ' '))] = Uri.UnescapeDataString(pair.Substring(eq + 1).Replace('+', ' '));
            }
            return query;
        }

        /// <summary>The page the browser shows after Google: the game has the answer, the tab can be closed.</summary>
        private static async Task RespondAsync(TcpClient client, int status, bool? signedIn, bool english)
        {
            string title = signedIn == true ? (english ? "You are signed in" : "Giriş yapıldı")
                : signedIn == false ? (english ? "Sign-in cancelled" : "Giriş iptal edildi") : "";
            string line = signedIn == true ? (english ? "Return to Galaxy 101 — you can close this tab." : "Galaxy 101'e dönebilirsin — bu sekmeyi kapatabilirsin.")
                : signedIn == false ? (english ? "Return to Galaxy 101 to try again." : "Yeniden denemek için Galaxy 101'e dön.") : "";
            string html = signedIn == null ? "" :
                "<!doctype html><html lang=\"" + (english ? "en" : "tr") + "\"><head><meta charset=\"utf-8\"><title>Galaxy 101</title>" +
                "<style>body{margin:0;height:100vh;display:grid;place-items:center;background:#0B1030;color:#F4F1E8;font:700 18px system-ui,sans-serif}" +
                "div{text-align:center}h1{color:#FFC94A;font-size:28px;margin:0 0 10px}p{color:#A9B4D0;margin:0}</style></head>" +
                "<body><div><h1>" + WebUtility.HtmlEncode(title) + "</h1><p>" + WebUtility.HtmlEncode(line) + "</p></div></body></html>";
            byte[] body = Encoding.UTF8.GetBytes(html);
            string reason = status == 200 ? "OK" : status == 404 ? "Not Found" : "Bad Request";
            byte[] header = Encoding.ASCII.GetBytes("HTTP/1.1 " + status + " " + reason + "\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: " + body.Length +
                "\r\nCache-Control: no-store\r\nReferrer-Policy: no-referrer\r\nConnection: close\r\n\r\n");
            try
            {
                var stream = client.GetStream();
                await stream.WriteAsync(header, 0, header.Length);
                await stream.WriteAsync(body, 0, body.Length);
                await stream.FlushAsync();
            }
            catch (IOException) { /* the browser went away: the code (if any) is already in hand */ }
        }

        private static bool FixedTimeEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        private static byte[] Random(int length)
        {
            var bytes = new byte[length];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(bytes);
            return bytes;
        }

        public static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
