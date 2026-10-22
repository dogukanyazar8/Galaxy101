using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Galaxy101.Contracts;
using Galaxy101.Server.Economy;
using Galaxy101.Server.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.Tokens;

namespace Galaxy101.Server.Tests;

public sealed class AppleStoreVerifierTests
{
    private static readonly FakeTimeProvider Clock = new(DateTimeOffset.UtcNow);

    [Fact]
    public void Accepts_a_transaction_signed_through_the_pinned_root()
    {
        var chain = TestChain.Create();
        var verifier = Verifier(chain.Root);
        var jws = chain.Sign(new { bundleId = "app.galaxy101", productId = "galaxy101.gems_0", transactionId = "2000001", environment = "Production" });
        Assert.Equal(new VerifiedPurchase("2000001", "galaxy101.gems_0"), verifier.Verify("galaxy101.gems_0", jws));
    }

    [Fact]
    public void Rejects_other_roots_products_bundles_refunds_and_tampering()
    {
        var chain = TestChain.Create();
        var good = new { bundleId = "app.galaxy101", productId = "galaxy101.gems_0", transactionId = "1", environment = "Production" };
        Assert.Null(Verifier(TestChain.Create().Root).Verify("galaxy101.gems_0", chain.Sign(good)));
        Assert.Null(Verifier(chain.Root).Verify("galaxy101.gems_3", chain.Sign(good)));
        Assert.Null(Verifier(chain.Root).Verify("galaxy101.gems_0", chain.Sign(good with { })[..^4] + "AAAA"));
        Assert.Null(Verifier(chain.Root).Verify("galaxy101.gems_0", chain.Sign(new { bundleId = "other.app", productId = "galaxy101.gems_0", transactionId = "1", environment = "Production" })));
        Assert.Null(Verifier(chain.Root).Verify("galaxy101.gems_0", chain.Sign(new { bundleId = "app.galaxy101", productId = "galaxy101.gems_0", transactionId = "1", environment = "Production", revocationDate = 1L })));
        Assert.Null(Verifier(chain.Root).Verify("galaxy101.gems_0", "not.a.jws"));
        // Without Apple's marker extensions the chain is a generic certificate chain, not a StoreKit one.
        var unmarked = TestChain.Create(markers: false);
        Assert.Null(Verifier(unmarked.Root).Verify("galaxy101.gems_0", unmarked.Sign(good)));
    }

    private static AppleStoreVerifier Verifier(X509Certificate2 root) => new(Options.Create(new StoreOptions
    {
        AppleBundleId = "app.galaxy101", AppleRootCertificate = Convert.ToBase64String(root.RawData),
    }), Clock);

    private sealed record TestChain(X509Certificate2 Root, X509Certificate2 Intermediate, X509Certificate2 Leaf, ECDsa LeafKey)
    {
        public static TestChain Create(bool markers = true)
        {
            var now = DateTimeOffset.UtcNow;
            using var rootKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var rootRequest = new CertificateRequest("CN=Test Root", rootKey, HashAlgorithmName.SHA256);
            rootRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            var root = rootRequest.CreateSelfSigned(now.AddDays(-1), now.AddYears(5));

            var intermediateKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var intermediateRequest = new CertificateRequest("CN=Test Intermediate", intermediateKey, HashAlgorithmName.SHA256);
            intermediateRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            if (markers) intermediateRequest.CertificateExtensions.Add(new X509Extension("1.2.840.113635.100.6.2.1", [5, 0], false));
            var intermediate = intermediateRequest.Create(root, now.AddDays(-1), now.AddYears(4), [1]).CopyWithPrivateKey(intermediateKey);

            var leafKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var leafRequest = new CertificateRequest("CN=Test Leaf", leafKey, HashAlgorithmName.SHA256);
            if (markers) leafRequest.CertificateExtensions.Add(new X509Extension("1.2.840.113635.100.6.11.1", [5, 0], false));
            var leaf = leafRequest.Create(intermediate, now.AddDays(-1), now.AddYears(3), [2]);
            return new TestChain(root, intermediate, leaf, leafKey);
        }

        public string Sign(object payload)
        {
            var header = JsonSerializer.Serialize(new { alg = "ES256", x5c = new[] { Leaf, Intermediate, Root }.Select(c => Convert.ToBase64String(c.RawData)) });
            var signingInput = Base64UrlEncoder.Encode(header) + "." + Base64UrlEncoder.Encode(JsonSerializer.Serialize(payload));
            return signingInput + "." + Base64UrlEncoder.Encode(LeafKey.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256));
        }
    }
}

[Collection(ServerCollection.Name)]
public sealed class StorePurchaseTests(ServerFixture server)
{
    private sealed class FakeStore : IStoreVerifier
    {
        // Receipt format for tests: "ok:<transaction>" is paid, "down" is an outage, anything else is invalid.
        public Task<VerifiedPurchase?> VerifyAsync(string platform, string productId, string receipt, CancellationToken ct) =>
            receipt == "down" ? throw new StoreUnavailableException("down")
            : Task.FromResult(receipt.StartsWith("ok:") ? new VerifiedPurchase(receipt[3..], productId) : null);
    }

    private HttpClient WithFakeStore(string accessToken)
    {
        var client = server.WithWebHostBuilder(b => b.ConfigureServices(s => { s.RemoveAll<IStoreVerifier>(); s.AddScoped<IStoreVerifier, FakeStore>(); })).CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", accessToken);
        return client;
    }

    [Fact]
    public async Task Verified_receipt_grants_once_and_cannot_be_reused()
    {
        var (_, tokens) = await server.GuestAsync();
        var client = WithFakeStore(tokens.AccessToken);
        var receipt = "ok:" + Guid.NewGuid();
        var purchase = new StorePurchaseRequest { Platform = "google", ProductId = "galaxy101.gems_1", Receipt = receipt };

        var first = (await (await client.PostAsJsonAsync("/v1/shop/store-purchase", purchase)).Content.ReadFromJsonAsync<PurchaseResponse>())!;
        Assert.Equal(20 + 550, first.Wallet.Gems);
        Assert.False(first.AlreadyProcessed);

        // The client retrying after a lost response gets the same wallet, no second grant.
        var retry = (await (await client.PostAsJsonAsync("/v1/shop/store-purchase", purchase)).Content.ReadFromJsonAsync<PurchaseResponse>())!;
        Assert.True(retry.AlreadyProcessed);
        Assert.Equal(20 + 550, retry.Wallet.Gems);

        var (_, thief) = await server.GuestAsync();
        var stolen = await WithFakeStore(thief.AccessToken).PostAsJsonAsync("/v1/shop/store-purchase", purchase);
        Assert.Equal(HttpStatusCode.Conflict, stolen.StatusCode);
        Assert.Equal(ErrorCodes.ReceiptUsed, (await stolen.Content.ReadFromJsonAsync<ErrorResponse>())!.Code);
    }

    [Fact]
    public async Task Deal_grants_chips_and_gems_together()
    {
        var (_, tokens) = await server.GuestAsync();
        var response = await WithFakeStore(tokens.AccessToken).PostAsJsonAsync("/v1/shop/store-purchase",
            new StorePurchaseRequest { Platform = "apple", ProductId = "galaxy101.deal", Receipt = "ok:" + Guid.NewGuid() });
        var wallet = (await response.Content.ReadFromJsonAsync<PurchaseResponse>())!.Wallet;
        Assert.Equal(10_000 + 1_000_000, wallet.Chips);
        Assert.Equal(20 + 50, wallet.Gems);
    }

    [Fact]
    public async Task Invalid_receipts_unknown_products_and_outages_grant_nothing()
    {
        var (_, tokens) = await server.GuestAsync();
        var client = WithFakeStore(tokens.AccessToken);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/v1/shop/store-purchase", new StorePurchaseRequest { Platform = "google", ProductId = "galaxy101.gems_0", Receipt = "forged" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/v1/shop/store-purchase", new StorePurchaseRequest { Platform = "google", ProductId = "free.gems", Receipt = "ok:1" })).StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.PostAsJsonAsync("/v1/shop/store-purchase", new StorePurchaseRequest { Platform = "google", ProductId = "galaxy101.gems_0", Receipt = "down" })).StatusCode);
        Assert.Equal(20, (await client.GetFromJsonAsync<WalletResponse>("/v1/wallet"))!.Gems);
    }

    [Fact]
    public async Task Unconfigured_store_answers_unavailable()
    {
        var (client, _) = await server.GuestAsync();
        var response = await client.PostAsJsonAsync("/v1/shop/store-purchase", new StorePurchaseRequest { Platform = "google", ProductId = "galaxy101.gems_0", Receipt = "x" });
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}
