using Galaxy101.Contracts;
using Galaxy101.Server.Data;
using Galaxy101.Server.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Galaxy101.Server.Economy;

public static class ShopEndpoints
{
    public static void MapShop(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v1/wallet", async (HttpContext http, Ledger ledger, CancellationToken ct) =>
            Results.Ok(await ledger.DescribeAsync(http.User.AccountId(), ct))).RequireAuthorization();

        var shop = app.MapGroup("/v1/shop").RequireAuthorization();

        shop.MapGet("/catalog", async (HttpContext http, GameDb db, Catalog catalog, CancellationToken ct) =>
        {
            var me = http.User.AccountId();
            var owned = (await db.OwnedItems.Where(x => x.AccountId == me).Select(x => x.ItemId).ToListAsync(ct)).ToHashSet();
            return Results.Ok(new CatalogResponse
            {
                Chips = [.. catalog.Chips.Select(o => Store(o, "chips"))],
                Gems = [.. catalog.Gems.Select(o => Store(o, "gems"))],
                Skins = [.. catalog.Skins.Select(s => new OfferResponse { Id = s.Id, Kind = "skin", GemPrice = s.GemPrice, Popular = s.Popular, Owned = owned.Contains(s.Id) })],
                Deal = Store(catalog.Deal, "deal"),
            });
        });

        // Gem-priced items. The charge and the ownership row commit together or not at all.
        shop.MapPost("/purchase", async (GemPurchaseRequest request, HttpContext http, GameDb db, Ledger ledger, Catalog catalog, TimeProvider clock, CancellationToken ct) =>
        {
            var skin = catalog.Skins.FirstOrDefault(x => x.Id == request.OfferId);
            if (skin is null) return Api.NotFound();
            var me = http.User.AccountId();
            if (await db.OwnedItems.AnyAsync(x => x.AccountId == me && x.ItemId == skin.Id, ct)) return Api.Conflict(ErrorCodes.AlreadyOwned);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            try
            {
                var result = await ledger.ApplyAsync(me, 0, -skin.GemPrice, "item", "item:" + skin.Id, ct);
                if (result.Outcome == LedgerOutcome.InsufficientFunds) return Api.Conflict(ErrorCodes.InsufficientFunds);
                if (result.Outcome == LedgerOutcome.Duplicate) return Api.Conflict(ErrorCodes.AlreadyOwned);
                db.OwnedItems.Add(new OwnedItem { AccountId = me, ItemId = skin.Id, AcquiredAt = clock.GetUtcNow() });
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (DbUpdateException error) when (error.IsUniqueViolation())
            {
                // A concurrent purchase of the same item won; this one rolls back with no charge.
                return Api.Conflict(ErrorCodes.AlreadyOwned);
            }
            return Results.Ok(new PurchaseResponse { OfferId = skin.Id, Wallet = await ledger.DescribeAsync(me, ct) });
        });
    }

    private static OfferResponse Store(StoreOffer o, string kind) => new()
    {
        Id = o.Id, Kind = kind, Chips = o.Chips, Gems = o.Gems, BonusPercent = o.BonusPercent, StoreProductId = o.StoreProductId,
        ReferencePrice = o.ReferencePrice, ReferenceOldPrice = o.ReferenceOldPrice, Popular = o.Popular,
    };
}
