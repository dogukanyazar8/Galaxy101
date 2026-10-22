using System.Net;
using System.Net.Http.Json;
using Galaxy101.Contracts;
using Galaxy101.Server.Data;
using Galaxy101.Server.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Galaxy101.Server.Tests;

[Collection(ServerCollection.Name)]
public sealed class EconomyTests(ServerFixture server)
{
    [Fact]
    public async Task Catalog_matches_the_reference_shop()
    {
        var (client, _) = await server.GuestAsync();
        var catalog = (await client.GetFromJsonAsync<CatalogResponse>("/v1/shop/catalog"))!;
        Assert.Equal(4, catalog.Chips.Count);
        Assert.Equal(1_000_000, catalog.Chips.Single(x => x.Popular).Chips);
        Assert.All(catalog.Gems, g => Assert.StartsWith("galaxy101.", g.StoreProductId));
        Assert.Equal(800, catalog.Skins[0].GemPrice);
        Assert.Equal(1_000_000, catalog.Deal.Chips);
    }

    [Fact]
    public async Task Skin_purchase_needs_gems_and_happens_once()
    {
        var (client, tokens) = await server.GuestAsync();
        var poor = await client.PostAsJsonAsync("/v1/shop/purchase", new GemPurchaseRequest { OfferId = "skin-0" });
        Assert.Equal(HttpStatusCode.Conflict, poor.StatusCode);
        Assert.Equal(ErrorCodes.InsufficientFunds, (await poor.Content.ReadFromJsonAsync<ErrorResponse>())!.Code);

        await Grant(tokens.AccountId, gems: 1000);
        var bought = (await (await client.PostAsJsonAsync("/v1/shop/purchase", new GemPurchaseRequest { OfferId = "skin-0" })).Content.ReadFromJsonAsync<PurchaseResponse>())!;
        Assert.Equal(20 + 1000 - 800, bought.Wallet.Gems);
        Assert.Contains("skin-0", bought.Wallet.OwnedItems);

        var again = await client.PostAsJsonAsync("/v1/shop/purchase", new GemPurchaseRequest { OfferId = "skin-0" });
        Assert.Equal(ErrorCodes.AlreadyOwned, (await again.Content.ReadFromJsonAsync<ErrorResponse>())!.Code);
    }

    [Fact]
    public async Task Concurrent_purchases_charge_once()
    {
        var (client, tokens) = await server.GuestAsync();
        await Grant(tokens.AccountId, gems: 5000);
        var attempts = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PostAsJsonAsync("/v1/shop/purchase", new GemPurchaseRequest { OfferId = "skin-1" })));
        Assert.Single(attempts, x => x.StatusCode == HttpStatusCode.OK);
        var wallet = (await client.GetFromJsonAsync<WalletResponse>("/v1/wallet"))!;
        Assert.Equal(20 + 5000 - 1200, wallet.Gems);
    }

    [Fact]
    public async Task Ledger_never_goes_negative_and_is_idempotent()
    {
        var (_, tokens) = await server.GuestAsync();
        await using var scope = server.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<Ledger>();
        Assert.Equal(LedgerOutcome.InsufficientFunds, (await ledger.ApplyAsync(tokens.AccountId, -10_001, 0, "test", "t1", default)).Outcome);
        Assert.Equal(LedgerOutcome.Applied, (await ledger.ApplyAsync(tokens.AccountId, -10_000, 0, "test", "t2", default)).Outcome);
        Assert.Equal(LedgerOutcome.Duplicate, (await ledger.ApplyAsync(tokens.AccountId, -1, 0, "test", "t2", default)).Outcome);
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        Assert.Equal(0, (await db.Wallets.SingleAsync(x => x.AccountId == tokens.AccountId)).Chips);
        Assert.Equal(0, await db.Ledger.Where(x => x.AccountId == tokens.AccountId && x.Currency == "chips").SumAsync(x => x.Delta));
    }

    [Fact]
    public async Task Daily_streak_follows_the_server_calendar()
    {
        var (client, tokens) = await server.GuestAsync();
        var start = (await client.GetFromJsonAsync<DailyResponse>("/v1/daily"))!;
        Assert.True(start.CanClaimStreak);
        Assert.True(start.Streak[0].Today);

        var claimed = (await (await client.PostAsync("/v1/daily/claim", null)).Content.ReadFromJsonAsync<DailyResponse>())!;
        Assert.Equal(10_000 + 1000, claimed.Wallet.Chips);
        Assert.False(claimed.CanClaimStreak);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/v1/daily/claim", null)).StatusCode);

        tokens = await server.AdvanceAsync(TimeSpan.FromDays(1), client, tokens);
        var second = (await (await client.PostAsync("/v1/daily/claim", null)).Content.ReadFromJsonAsync<DailyResponse>())!;
        Assert.Equal(10_000 + 1000 + 2000, second.Wallet.Chips);
        Assert.True(second.Streak[1].Claimed);

        // Missing a day restarts the streak from day one.
        await server.AdvanceAsync(TimeSpan.FromDays(2), client, tokens);
        var restarted = (await client.GetFromJsonAsync<DailyResponse>("/v1/daily"))!;
        Assert.True(restarted.Streak[0].Today);
        Assert.DoesNotContain(restarted.Streak, x => x.Claimed);
    }

    [Fact]
    public async Task Quests_pay_out_only_when_complete_and_once()
    {
        var (client, tokens) = await server.GuestAsync();
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/v1/daily/quests/handsPlayed/claim", null)).StatusCode);

        await using (var scope = server.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            await using var tx = await db.Database.BeginTransactionAsync();
            await scope.ServiceProvider.GetRequiredService<DailyService>().AddProgressAsync(tokens.AccountId, new QuestCounters(0, 0, 5, 0, 0), default);
            await tx.CommitAsync();
        }
        var done = (await (await client.PostAsync("/v1/daily/quests/handsPlayed/claim", null)).Content.ReadFromJsonAsync<DailyResponse>())!;
        Assert.True(done.Quests.Single(x => x.Id == "handsPlayed").Claimed);
        Assert.Equal(10_000 + 1000, done.Wallet.Chips);
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync("/v1/daily/quests/handsPlayed/claim", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/v1/daily/quests/nope/claim", null)).StatusCode);
    }

    private async Task Grant(Guid account, long chips = 0, long gems = 0)
    {
        await using var scope = server.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<Ledger>().ApplyAsync(account, chips, gems, "test", "grant:" + Guid.NewGuid(), default);
    }
}
