using System.Text.Json.Serialization;
using Galaxy101.Server.Infrastructure;
using Microsoft.Extensions.Options;

namespace Galaxy101.Server.Auth;

public interface IGoogleCodeExchange
{
    /// <summary>
    /// Exchanges a desktop authorization code (with its PKCE verifier and loopback redirect) for Google's id token, or
    /// null when Google refuses it. Throws <see cref="ExternalProviderUnavailableException"/> when Google cannot be reached.
    /// </summary>
    Task<string?> ExchangeAsync(string code, string codeVerifier, string redirectUri, CancellationToken ct);
}

public sealed class ExternalProviderUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Google's OAuth token endpoint, called with the desktop client's secret (RFC 8252 loopback + RFC 7636 PKCE).</summary>
public sealed class GoogleCodeExchange(IHttpClientFactory httpFactory, IOptions<AuthOptions> options) : IGoogleCodeExchange
{
    public const string HttpClientName = "google-oauth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";

    public async Task<string?> ExchangeAsync(string code, string codeVerifier, string redirectUri, CancellationToken ct)
    {
        var auth = options.Value;
        if (!auth.GoogleDesktopEnabled) return null;
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code", ["code"] = code, ["code_verifier"] = codeVerifier, ["redirect_uri"] = redirectUri,
            ["client_id"] = auth.GoogleDesktopClientId, ["client_secret"] = auth.GoogleDesktopClientSecret,
        });
        try
        {
            using var response = await httpFactory.CreateClient(HttpClientName).PostAsync(TokenEndpoint, content, ct);
            // 400 invalid_grant: an expired, reused or forged code, or a verifier that does not match.
            if ((int)response.StatusCode is >= 400 and < 500) return null;
            if (!response.IsSuccessStatusCode) throw new ExternalProviderUnavailableException($"Google OAuth answered {(int)response.StatusCode}.");
            var reply = await response.Content.ReadFromJsonAsync<TokenReply>(ct);
            return string.IsNullOrEmpty(reply?.IdToken) ? null : reply.IdToken;
        }
        catch (HttpRequestException error) { throw new ExternalProviderUnavailableException("Google OAuth is unreachable.", error); }
        catch (TaskCanceledException error) when (!ct.IsCancellationRequested) { throw new ExternalProviderUnavailableException("Google OAuth timed out.", error); }
    }

    /// <summary>Only loopback redirects (RFC 8252 §7.3): the code can only have been caught on the player's own machine.</summary>
    public static bool IsLoopbackRedirect(string? uri) =>
        Uri.TryCreate(uri, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttp && (u.Host == "127.0.0.1" || u.Host == "[::1]")
        && !u.IsDefaultPort && string.IsNullOrEmpty(u.Query) && string.IsNullOrEmpty(u.Fragment) && uri!.Length <= 64;

    private sealed class TokenReply
    {
        [JsonPropertyName("id_token")] public string? IdToken { get; set; }
    }
}
