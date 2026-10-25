using Galaxy101.Contracts;
using Galaxy101.Server.Data;
using Microsoft.EntityFrameworkCore;

namespace Galaxy101.Server.Economy;

public enum LedgerOutcome { Applied, InsufficientFunds, Duplicate }

public readonly record struct LedgerResult(LedgerOutcome Outcome, long Chips, long Gems);

/// <summary>
/// The only writer of balances. A change is one conditional UPDATE (never below zero, no read-modify-write race)
/// plus append-only ledger rows keyed by an idempotency key, so a retried operation is applied at most once.
/// The balance and its rows commit together (inside the caller's transaction, or one of their own); callers that combine
/// several changes wrap them in one transaction.
/// </summary>
public sealed class Ledger(GameDb db, TimeProvider clock)
{
    public const long MaxDelta = 1_000_000_000_000;

    private sealed record Balance(long Chips, long Gems);

    public async Task<LedgerResult> ApplyAsync(Guid account, long chips, long gems, string reason, string key, CancellationToken ct)
    {
        if (Math.Abs(chips) > MaxDelta || Math.Abs(gems) > MaxDelta) throw new ArgumentOutOfRangeException(nameof(chips));
        if (db.Database.CurrentTransaction is not null) return await ApplyInTransactionAsync(account, chips, gems, reason, key, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var result = await ApplyInTransactionAsync(account, chips, gems, reason, key, ct);
        await tx.CommitAsync(ct);
        return result;
    }

    private async Task<LedgerResult> ApplyInTransactionAsync(Guid account, long chips, long gems, string reason, string key, CancellationToken ct)
    {
        string[] keys = [key + "#chips", key + "#gems", key + "#none"];
        if (await db.Ledger.AnyAsync(x => x.AccountId == account && keys.Contains(x.IdempotencyKey), ct))
        {
            var current = await GetAsync(account, ct);
            return new LedgerResult(LedgerOutcome.Duplicate, current.Chips, current.Gems);
        }
        var rows = await db.Database.SqlQuery<Balance>($"""
            UPDATE wallets SET chips = chips + {chips}, gems = gems + {gems}
            WHERE account_id = {account} AND chips + {chips} >= 0 AND gems + {gems} >= 0
            RETURNING chips AS "Chips", gems AS "Gems"
            """).ToListAsync(ct);
        if (rows.Count == 0)
        {
            var current = await GetAsync(account, ct);
            return new LedgerResult(LedgerOutcome.InsufficientFunds, current.Chips, current.Gems);
        }
        var now = clock.GetUtcNow();
        // One row per touched currency; "#" separates the operation key from the currency suffix.
        if (chips != 0) db.Ledger.Add(Entry(account, "chips", chips, rows[0].Chips, reason, key, now));
        if (gems != 0) db.Ledger.Add(Entry(account, "gems", gems, rows[0].Gems, reason, key, now));
        if (chips == 0 && gems == 0) db.Ledger.Add(Entry(account, "none", 0, rows[0].Chips, reason, key, now));
        await db.SaveChangesAsync(ct);
        return new LedgerResult(LedgerOutcome.Applied, rows[0].Chips, rows[0].Gems);
    }

    public async Task<Wallet> GetAsync(Guid account, CancellationToken ct) =>
        await db.Wallets.AsNoTracking().FirstOrDefaultAsync(x => x.AccountId == account, ct) ?? new Wallet { AccountId = account };

    public async Task<WalletResponse> DescribeAsync(Guid account, CancellationToken ct)
    {
        var wallet = await GetAsync(account, ct);
        var items = await db.OwnedItems.Where(x => x.AccountId == account).OrderBy(x => x.AcquiredAt).Select(x => x.ItemId).ToListAsync(ct);
        return new WalletResponse { Chips = wallet.Chips, Gems = wallet.Gems, OwnedItems = items };
    }

    private static LedgerEntry Entry(Guid account, string currency, long delta, long after, string reason, string key, DateTimeOffset now) => new()
    {
        AccountId = account, Currency = currency, Delta = delta, BalanceAfter = after, Reason = reason,
        IdempotencyKey = key + "#" + currency, CreatedAt = now,
    };
}
