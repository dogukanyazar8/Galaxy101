using Galaxy101.Server.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Galaxy101.Server.Auth;

public sealed record ExternalIdentity(string Provider, string Subject);

public interface IExternalIdentityValidator
{
    /// <summary>Returns the verified identity, or null when the token is not valid for this game.</summary>
    Task<ExternalIdentity?> ValidateAsync(string provider, string idToken, CancellationToken ct);
}

/// <summary>Verifies Google and Sign in with Apple id tokens against the providers' published signing keys.</summary>
public sealed class OidcIdentityValidator(IOptions<AuthOptions> options) : IExternalIdentityValidator
{
    private sealed record Provider(ConfigurationManager<OpenIdConnectConfiguration> Keys, string[] Issuers, string[] Audiences);

    private static readonly JsonWebTokenHandler Handler = new();
    private readonly Dictionary<string, Provider> providers = new()
    {
        ["google"] = new(Manager("https://accounts.google.com/.well-known/openid-configuration"),
            ["https://accounts.google.com", "accounts.google.com"], options.Value.GoogleClientIds),
        ["apple"] = new(Manager("https://appleid.apple.com/.well-known/openid-configuration"),
            ["https://appleid.apple.com"], options.Value.AppleClientIds),
    };

    public async Task<ExternalIdentity?> ValidateAsync(string provider, string idToken, CancellationToken ct)
    {
        if (!providers.TryGetValue(provider ?? "", out var p) || p.Audiences.Length == 0 || string.IsNullOrEmpty(idToken) || idToken.Length > 8192) return null;
        var configuration = await p.Keys.GetConfigurationAsync(ct);
        var result = await Handler.ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidIssuers = p.Issuers,
            ValidAudiences = p.Audiences,
            IssuerSigningKeys = configuration.SigningKeys,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(2),
        });
        if (!result.IsValid)
        {
            // A rotated provider key: refresh once and let the client retry.
            if (result.Exception is SecurityTokenSignatureKeyNotFoundException) p.Keys.RequestRefresh();
            return null;
        }
        return result.Claims.TryGetValue(JwtRegisteredClaimNames.Sub, out var sub) && sub is string subject && subject.Length is > 0 and <= 255
            ? new ExternalIdentity(provider!, subject) : null;
    }

    private static ConfigurationManager<OpenIdConnectConfiguration> Manager(string url) =>
        new(url, new OpenIdConnectConfigurationRetriever(), new HttpDocumentRetriever { RequireHttps = true });
}
