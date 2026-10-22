using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Galaxy101.Server.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Galaxy101.Server.Economy;

public sealed record VerifiedPurchase(string TransactionId, string ProductId);

/// <summary>The store could not be asked (not configured, network, 5xx). Distinct from a receipt that is invalid.</summary>
public sealed class StoreUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

public interface IStoreVerifier
{
    /// <summary>Returns the purchase when the store confirms it is paid and not refunded; null when it is not valid.</summary>
    Task<VerifiedPurchase?> VerifyAsync(string platform, string productId, string receipt, CancellationToken ct);
}

public sealed class StoreVerifier(GooglePlayVerifier google, AppleStoreVerifier apple) : IStoreVerifier
{
    public Task<VerifiedPurchase?> VerifyAsync(string platform, string productId, string receipt, CancellationToken ct) => platform switch
    {
        "google" => google.VerifyAsync(productId, receipt, ct),
        "apple" => Task.FromResult(apple.Verify(productId, receipt)),
        _ => Task.FromResult<VerifiedPurchase?>(null),
    };
}

/// <summary>Google Play Developer API <c>purchases.products.get</c> with a service-account OAuth token.</summary>
public sealed class GooglePlayVerifier(IHttpClientFactory httpFactory, IOptions<StoreOptions> options, TimeProvider clock)
{
    public const string HttpClientName = "google-play";
    private readonly SemaphoreSlim tokenGate = new(1, 1);
    private (string Token, DateTimeOffset Expires)? token;

    private sealed record ServiceAccount(string client_email, string private_key, string token_uri);
    private sealed record TokenReply(string access_token, int expires_in);
    // purchaseState 0 = purchased, 1 = cancelled, 2 = pending.
    private sealed record ProductPurchase(int? purchaseState, string? orderId, string? productId);

    public async Task<VerifiedPurchase?> VerifyAsync(string productId, string purchaseToken, CancellationToken ct)
    {
        var store = options.Value;
        if (string.IsNullOrEmpty(store.GooglePackageName) || string.IsNullOrEmpty(store.GoogleServiceAccountJson))
            throw new StoreUnavailableException("Google Play verification is not configured.");
        if (string.IsNullOrEmpty(purchaseToken) || purchaseToken.Length > 4096) return null;
        var url = $"https://androidpublisher.googleapis.com/androidpublisher/v3/applications/{Uri.EscapeDataString(store.GooglePackageName)}" +
                  $"/purchases/products/{Uri.EscapeDataString(productId)}/tokens/{Uri.EscapeDataString(purchaseToken)}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await AccessTokenAsync(store, ct));
        using var response = await Send(request, ct);
        if (response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.Gone) return null;
        if (!response.IsSuccessStatusCode) throw new StoreUnavailableException($"Google Play answered {(int)response.StatusCode}.");
        var purchase = await response.Content.ReadFromJsonAsync<ProductPurchase>(ct);
        if (purchase is not { purchaseState: 0 }) return null;
        // orderId is unique per paid order; license testers' purchases have one too.
        return new VerifiedPurchase(purchase.orderId ?? purchaseToken, productId);
    }

    private async Task<string> AccessTokenAsync(StoreOptions store, CancellationToken ct)
    {
        await tokenGate.WaitAsync(ct);
        try
        {
            if (token is { } cached && cached.Expires > clock.GetUtcNow().AddMinutes(1)) return cached.Token;
            var account = JsonSerializer.Deserialize<ServiceAccount>(store.GoogleServiceAccountJson)
                ?? throw new StoreUnavailableException("Invalid Google service account JSON.");
            using var rsa = RSA.Create();
            rsa.ImportFromPem(account.private_key);
            var now = clock.GetUtcNow();
            var assertion = new Microsoft.IdentityModel.JsonWebTokens.JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Issuer = account.client_email,
                Audience = account.token_uri,
                Claims = new Dictionary<string, object> { ["scope"] = "https://www.googleapis.com/auth/androidpublisher" },
                IssuedAt = now.UtcDateTime, NotBefore = now.UtcDateTime, Expires = now.AddMinutes(30).UtcDateTime,
                SigningCredentials = new SigningCredentials(new RsaSecurityKey(rsa.ExportParameters(true)), SecurityAlgorithms.RsaSha256),
            });
            using var request = new HttpRequestMessage(HttpMethod.Post, account.token_uri)
            {
                Content = new FormUrlEncodedContent([new("grant_type", "urn:ietf:params:oauth:grant-type:jwt-bearer"), new("assertion", assertion)]),
            };
            using var response = await Send(request, ct);
            if (!response.IsSuccessStatusCode) throw new StoreUnavailableException($"Google OAuth answered {(int)response.StatusCode}.");
            var reply = await response.Content.ReadFromJsonAsync<TokenReply>(ct) ?? throw new StoreUnavailableException("Empty Google OAuth reply.");
            token = (reply.access_token, now.AddSeconds(reply.expires_in));
            return reply.access_token;
        }
        finally { tokenGate.Release(); }
    }

    private async Task<HttpResponseMessage> Send(HttpRequestMessage request, CancellationToken ct)
    {
        try { return await httpFactory.CreateClient(HttpClientName).SendAsync(request, ct); }
        catch (HttpRequestException error) { throw new StoreUnavailableException("Google Play is unreachable.", error); }
    }
}

/// <summary>
/// StoreKit 2 signed transactions (JWS, ES256): verified offline against the pinned Apple Root CA - G3 through the
/// x5c chain, with Apple's marker OIDs on the leaf and intermediate, then bundle / product / refund checks.
/// </summary>
public sealed class AppleStoreVerifier(IOptions<StoreOptions> options, TimeProvider clock)
{
    private const string LeafMarker = "1.2.840.113635.100.6.11.1";
    private const string IntermediateMarker = "1.2.840.113635.100.6.2.1";

    private sealed record Transaction(string? bundleId, string? productId, string? transactionId, string? environment, long? revocationDate);

    public VerifiedPurchase? Verify(string productId, string jws)
    {
        var store = options.Value;
        if (string.IsNullOrEmpty(store.AppleBundleId) || string.IsNullOrEmpty(store.AppleRootCertificate))
            throw new StoreUnavailableException("App Store verification is not configured.");
        if (string.IsNullOrEmpty(jws) || jws.Length > 16384) return null;
        var parts = jws.Split('.');
        if (parts.Length != 3) return null;
        try
        {
            using var header = JsonDocument.Parse(Base64UrlEncoder.DecodeBytes(parts[0]));
            if (header.RootElement.GetProperty("alg").GetString() != "ES256") return null;
            var x5c = header.RootElement.GetProperty("x5c").EnumerateArray().Select(x => X509CertificateLoader.LoadCertificate(Convert.FromBase64String(x.GetString()!))).ToArray();
            if (x5c.Length < 2 || !TrustedChain(x5c, X509CertificateLoader.LoadCertificate(Convert.FromBase64String(store.AppleRootCertificate)))) return null;
            using var key = x5c[0].GetECDsaPublicKey();
            if (key is null || !key.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), Base64UrlEncoder.DecodeBytes(parts[2]), HashAlgorithmName.SHA256)) return null;
            var t = JsonSerializer.Deserialize<Transaction>(Base64UrlEncoder.DecodeBytes(parts[1]));
            if (t is null || t.bundleId != store.AppleBundleId || t.productId != productId || string.IsNullOrEmpty(t.transactionId) || t.revocationDate is not null) return null;
            if (t.environment != "Production" && !(store.AppleAllowSandbox && t.environment == "Sandbox")) return null;
            return new VerifiedPurchase(t.transactionId, productId);
        }
        catch (Exception error) when (error is FormatException or JsonException or KeyNotFoundException or InvalidOperationException or CryptographicException)
        {
            return null;
        }
    }

    private bool TrustedChain(X509Certificate2[] x5c, X509Certificate2 root)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.CustomTrustStore.Add(root);
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationTime = clock.GetUtcNow().UtcDateTime;
        foreach (var c in x5c.Skip(1)) chain.ChainPolicy.ExtraStore.Add(c);
        return chain.Build(x5c[0]) && chain.ChainElements[^1].Certificate.Thumbprint == root.Thumbprint &&
               x5c[0].Extensions[LeafMarker] is not null && x5c[1].Extensions[IntermediateMarker] is not null;
    }
}
