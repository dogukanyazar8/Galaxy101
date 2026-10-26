using System.Text.Json;
using Galaxy101.Contracts;
using Galaxy101.Server.Data;
using Galaxy101.Server.Economy;
using Galaxy101.Server.Infrastructure;
using Galaxy101.Server.Profile;
using Microsoft.EntityFrameworkCore;

namespace Galaxy101.Server.V4;

/// <summary>What the hub shows of an account: the package's User card plus the Me fields (protocol.json Me).</summary>
public sealed record Card(Guid Id, string Name, string Ini, string Color, int Level, int Frame, string Status, int RankIdx, string Code,
    long Xp, long XpNext, long Chips, long Gems, string Skin, List<string> Skins, V4Rank Rank, int SeasonLp);

/// <summary>A seat's share of a finished v4 match, as stored (participants, stats, ledger, leaderboards, quests).</summary>
public sealed record SeatSettlement(int Seat, Guid? Account, int Place, int Total, long Payout, long Stake, int? LpDelta, int Weekly, long Xp,
    QuestCounters Counters, IReadOnlyList<bool> HandWins, bool Left);

/// <summary>
/// Durable side of the v4 tables. Every call uses its own short-lived DbContext (the hub is a long-lived singleton).
/// Ledger keys are <c>v4:{table}:…</c>, so the money a table holds can always be found again (restart refunds).
/// </summary>
public sealed class Store(IServiceScopeFactory scopes, GameCalendar calendar)
{
    public const string Engine = "v4";

    public async Task<Card?> CardAsync(Guid account)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var row = await (from a in db.Accounts.AsNoTracking()
                         join s in db.Stats on a.Id equals s.AccountId
                         join w in db.Wallets on a.Id equals w.AccountId
                         where a.Id == account
                         select new { a.Name, a.Color, a.Frame, a.Status, a.FriendCode, a.SettingsJson, s.Xp, s.Lp, s.LpSeason, s.BestLp, s.Shield, w.Chips, w.Gems })
            .FirstOrDefaultAsync();
        if (row is null) return null;
        var owned = await db.OwnedItems.AsNoTracking().Where(o => o.AccountId == account && o.ItemId.StartsWith("skin")).Select(o => o.ItemId).ToListAsync();
        var lp = Ranked.SeasonLp(row.Lp, row.LpSeason, calendar.Season);
        var (level, into, next) = Progression.Level(row.Xp);
        string skin = "classic";
        try
        {
            using var settings = JsonDocument.Parse(string.IsNullOrEmpty(row.SettingsJson) ? "{}" : row.SettingsJson);
            if (settings.RootElement.ValueKind == JsonValueKind.Object && settings.RootElement.TryGetProperty("skin", out var s) && s.ValueKind == JsonValueKind.String) skin = s.GetString()!;
        }
        catch (JsonException) { }
        var idx = RankIdx(lp);
        var rank = new V4Rank { Idx = idx, Lp = lp - idx * Ranked.DivisionLp, Prot = row.Shield, Peak = RankIdx(Math.Max(lp, row.BestLp)) };
        return new Card(account, row.Name, Initials(row.Name), row.Color, level, row.Frame, row.Status, idx, row.FriendCode, into, next, row.Chips, row.Gems,
            skin, owned, rank, lp);
    }

    /// <summary>The package's rank index (division 0 … 14, 15 past Usta I): <c>idx × 100 + lp</c> is the season LP.</summary>
    public static int RankIdx(int lp) => Math.Clamp(lp / Ranked.DivisionLp, 0, Ranked.Divisions);

    /// <summary>mockServer initials(): the first letter of up to two words, Turkish upper case.</summary>
    public static string Initials(string name)
    {
        var tr = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
        var s = string.Concat(name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Take(2).Select(w => char.ToUpper(w[0], tr)));
        return s.Length == 0 ? "?" : s;
    }

    public async Task<LedgerResult> ApplyAsync(Guid account, long chips, string reason, string key)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Ledger>().ApplyAsync(account, chips, 0, reason, key, default);
    }

    /// <summary>
    /// A stake back (key <c>{stakeKey}:back</c>, applied at most once) — only if the ledger holds the stake: a stake write
    /// that failed may or may not have landed. Null when there is nothing to give back.
    /// </summary>
    public async Task<LedgerResult?> RefundStakeAsync(Guid account, long amount, string stakeKey)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var ledger = scope.ServiceProvider.GetRequiredService<Ledger>();
        await using var tx = await db.Database.BeginTransactionAsync();
        var charged = await db.Ledger.AnyAsync(l => l.AccountId == account && l.IdempotencyKey == stakeKey + "#chips" && l.Delta < 0);
        var back = charged ? await ledger.ApplyAsync(account, amount, 0, "refund", stakeKey + ":back", default) : (LedgerResult?)null;
        await tx.CommitAsync();
        return back;
    }

    public async Task<long> ChipsAsync(Guid account)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<GameDb>().Wallets.Where(w => w.AccountId == account).Select(w => w.Chips).FirstOrDefaultAsync();
    }

    public async Task<bool> FriendsAsync(Guid a, Guid b)
    {
        var (low, high) = Social.Players.Pair(a, b);
        await using var scope = scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<GameDb>().Friendships.AnyAsync(f => f.LowId == low && f.HighId == high && f.Accepted);
    }

    public async Task<bool> ExistsAsync(Guid account)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<GameDb>().Accounts.AnyAsync(a => a.Id == account);
    }

    /// <summary>Did the two play a match together recently (the invite list's "Son oynadıkların", existing rule: last 20 matches).</summary>
    public async Task<bool> RecentAsync(Guid a, Guid b)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var mine = db.MatchParticipants.Where(p => p.AccountId == a).OrderByDescending(p => p.FinishedAt).Select(p => p.MatchId).Take(20);
        return await db.MatchParticipants.AnyAsync(p => p.AccountId == b && mine.Contains(p.MatchId));
    }

    public async Task<HashSet<(Guid, Guid)>> BlocksAsync(IReadOnlyCollection<Guid> accounts)
    {
        await using var scope = scopes.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<GameDb>().Blocks.AsNoTracking()
            .Where(b => accounts.Contains(b.BlockerId) && accounts.Contains(b.BlockedId)).Select(b => new { b.BlockerId, b.BlockedId }).ToListAsync();
        return [.. rows.Select(b => (b.BlockerId, b.BlockedId))];
    }

    /// <summary>The table's durable record, written when it opens: the stakes it will hold are found by its id after a restart.</summary>
    public async Task CreateRecordAsync(Guid table, string mode, long stake, int hands, int turnSec, object options)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        db.Matches.Add(new MatchRecord
        {
            Id = table, Mode = mode, Stake = stake, Status = MatchStatus.Active, Seats = [Guid.Empty, Guid.Empty, Guid.Empty, Guid.Empty], Hands = hands,
            TurnSeconds = turnSec, OptionsJson = JsonSerializer.Serialize(options, Realtime.Connection.Json), CreatedAt = calendar.Now,
        });
        await db.SaveChangesAsync();
    }

    public async Task SeatsAsync(Guid table, Guid[] seats, int botMask, int firstDealer)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<GameDb>().Matches.Where(m => m.Id == table)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Seats, seats).SetProperty(m => m.BotMask, botMask).SetProperty(m => m.FirstStarter, firstDealer));
    }

    /// <summary>The table closed without a match (everyone left): nothing is held any more.</summary>
    public async Task CloseRecordAsync(Guid table)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<GameDb>().Matches.Where(m => m.Id == table && m.Status == MatchStatus.Active)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, MatchStatus.Aborted).SetProperty(m => m.FinishedAt, calendar.Now));
    }

    /// <summary>
    /// After a restart: every v4 table still open lost its in-memory state. Each account gets back what the table holds of
    /// it (its stakes minus their refunds, from the table's <c>bet:</c> ledger keys) and the record closes as aborted. Gifts
    /// sent at the table were spent, not held, and stay spent.
    /// </summary>
    public async Task<int> AbortOpenAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var ledger = scope.ServiceProvider.GetRequiredService<Ledger>();
        var open = (await db.Matches.AsNoTracking().Where(m => m.Status == MatchStatus.Active).Select(m => new { m.Id, m.OptionsJson }).ToListAsync(ct))
            .Where(m => IsV4(m.OptionsJson)).Select(m => m.Id).ToList();
        foreach (var id in open)
        {
            var prefix = $"v4:{id}:";
            var stakes = prefix + "bet:";
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var held = await db.Ledger.Where(l => l.Currency == "chips" && l.IdempotencyKey.StartsWith(stakes))
                .GroupBy(l => l.AccountId).Select(g => new { Account = g.Key, Net = g.Sum(l => l.Delta) }).ToListAsync(ct);
            foreach (var h in held.Where(h => h.Net < 0))
                await ledger.ApplyAsync(h.Account, -h.Net, 0, "refund", prefix + "abort", ct);
            await db.Matches.Where(m => m.Id == id).ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, MatchStatus.Aborted).SetProperty(m => m.FinishedAt, calendar.Now), ct);
            await tx.CommitAsync(ct);
        }
        return open.Count;
    }

    public static bool IsV4(string? optionsJson)
    {
        if (string.IsNullOrEmpty(optionsJson)) return false;
        try
        {
            using var doc = JsonDocument.Parse(optionsJson);
            return doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("engine", out var e) && e.ValueKind == JsonValueKind.String && e.GetString() == Engine;
        }
        catch (JsonException) { return false; }
    }

    /// <summary>Hand XP (R-93) at once, so a player who leaves keeps what the hands gave: the new level line for the xp event.</summary>
    public async Task<(long Xp, long Next, int Level, int LevelUp)?> AddXpAsync(Guid account, long gained)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var rows = await db.Database.SqlQuery<long>($"UPDATE stats SET xp = xp + {gained} WHERE account_id = {account} RETURNING xp AS \"Value\"").ToListAsync();
        if (rows.Count == 0) return null;
        var before = Progression.Level(rows[0] - gained).Level;
        var (level, into, next) = Progression.Level(rows[0]);
        return (into, next, level, level - before);
    }

    /// <summary>
    /// A leaver's quest progress, the hands they finished (counts, wins, mode, streak) and LP, recorded when they leave;
    /// their XP was written hand by hand. The match's own settlement later covers only whoever plays the seat then.
    /// </summary>
    public async Task LeaverAsync(Guid table, string mode, int seat, Guid account, int total, long stake, QuestCounters counters, IReadOnlyList<bool> handWins, int? lpDelta)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var daily = scope.ServiceProvider.GetRequiredService<DailyService>();
        var ledger = scope.ServiceProvider.GetRequiredService<Ledger>();
        await using var tx = await db.Database.BeginTransactionAsync();
        db.MatchParticipants.Add(new MatchParticipant
        {
            MatchId = table, Seat = seat, AccountId = account, Mode = mode, Place = 4, Total = total, ChipDelta = -stake, LpDelta = lpDelta, Left = true, FinishedAt = calendar.Now,
        });
        await db.SaveChangesAsync();
        // Lock order as everywhere: daily state, stats, wallet.
        await daily.AddProgressAsync(account, counters, default);
        var stats = (await db.Stats.FromSql($"SELECT * FROM stats WHERE account_id = {account} FOR UPDATE").ToListAsync()).Single();
        AddHands(stats, counters, handWins, Array.IndexOf(GameModes.Rated, mode));
        if (lpDelta is { } lp)
        {
            var season = calendar.Season;
            await Ranked.RollOverAsync(db, ledger, stats, season, default);
            (stats.Lp, stats.Shield) = Ranked.Apply(stats.Lp, stats.Shield, lp);
            stats.BestLp = Math.Max(stats.BestLp, stats.Lp);
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO leaderboard_scores (board, account_id, points) VALUES ({Social.LeaderboardEndpoints.SeasonBoard(season)}, {account}, {(long)stats.Lp})
                ON CONFLICT (board, account_id) DO UPDATE SET points = EXCLUDED.points
                """);
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    /// <summary>Profile counts of the hands a person played at a table: hands, wins, okey finishes, pairs openings, streak, per mode.</summary>
    private static void AddHands(PlayerStats stats, QuestCounters counters, IReadOnlyList<bool> handWins, int modeIndex)
    {
        stats.Hands += counters.HandsPlayed;
        stats.Wins += counters.WinHands;
        stats.OkeyFinishes += counters.OkeyFinishes;
        stats.PairsOpenings += counters.PairsOpenings;
        foreach (var won in handWins)
        {
            stats.CurrentStreak = won ? stats.CurrentStreak + 1 : 0;
            stats.BestStreak = Math.Max(stats.BestStreak, stats.CurrentStreak);
        }
        if (modeIndex >= 0)
        {
            stats.ModeHands = [.. stats.ModeHands.Select((v, i) => i == modeIndex ? v + counters.HandsPlayed : v)];
            stats.ModeWins = [.. stats.ModeWins.Select((v, i) => i == modeIndex ? v + counters.WinHands : v)];
        }
    }

    /// <summary>
    /// Pays out, updates stats, leaderboards, LP and quests, records the participants and closes the match: all or nothing,
    /// and once — a retry after a failure that hid a commit finds the match completed and changes nothing (false).
    /// </summary>
    public async Task<bool> SettleAsync(Guid table, string mode, Guid[] seats, int botMask, IReadOnlyList<SeatSettlement> outcome)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var ledger = scope.ServiceProvider.GetRequiredService<Ledger>();
        var daily = scope.ServiceProvider.GetRequiredService<DailyService>();
        var now = calendar.Now;
        var modeIndex = Array.IndexOf(GameModes.Rated, mode);
        var weeklyBoard = calendar.WeeklyBoard;
        var season = calendar.Season;
        var seasonBoard = Social.LeaderboardEndpoints.SeasonBoard(season);
        var accounts = outcome.Where(o => o.Account is not null).Select(o => o.Account!.Value).ToList();
        var existing = (await db.Accounts.Where(a => accounts.Contains(a.Id)).Select(a => a.Id).ToListAsync()).ToHashSet();

        await using var tx = await db.Database.BeginTransactionAsync();
        // Closing the record first also locks it: a second settlement of the same match waits here and then finds it done.
        var open = await db.Matches.Where(m => m.Id == table && m.Status != MatchStatus.Completed).ExecuteUpdateAsync(s => s
            .SetProperty(m => m.Status, MatchStatus.Completed).SetProperty(m => m.FinishedAt, now).SetProperty(m => m.Seats, seats).SetProperty(m => m.BotMask, botMask));
        if (open == 0) return false;
        foreach (var o in outcome)
        {
            var present = o.Account is { } id && existing.Contains(id);
            db.MatchParticipants.Add(new MatchParticipant
            {
                MatchId = table, Seat = o.Seat, AccountId = present ? o.Account : null, Mode = mode, Place = o.Place, Total = o.Total,
                ChipDelta = o.Payout - o.Stake, LpDelta = o.LpDelta, Xp = o.Xp, Left = o.Left, FinishedAt = now,
            });
            if (!present) continue;
            var account = o.Account!.Value;
            // Lock order as everywhere: daily state, stats, wallet.
            await daily.AddProgressAsync(account, o.Counters, default);
            var stats = (await db.Stats.FromSql($"SELECT * FROM stats WHERE account_id = {account} FOR UPDATE").ToListAsync()).Single();
            await Ranked.RollOverAsync(db, ledger, stats, season, default);
            if (o.Payout > 0) await ledger.ApplyAsync(account, o.Payout, 0, "payout", $"v4:{table}:payout", default);
            AddHands(stats, o.Counters, o.HandWins, modeIndex);
            if (o.LpDelta is { } lp)
            {
                (stats.Lp, stats.Shield) = Ranked.Apply(stats.Lp, stats.Shield, lp);
                stats.BestLp = Math.Max(stats.BestLp, stats.Lp);
                await db.Database.ExecuteSqlAsync($"""
                    INSERT INTO leaderboard_scores (board, account_id, points) VALUES ({seasonBoard}, {account}, {(long)stats.Lp})
                    ON CONFLICT (board, account_id) DO UPDATE SET points = EXCLUDED.points
                    """);
            }
            if (o.Weekly > 0)
                await db.Database.ExecuteSqlAsync($"""
                    INSERT INTO leaderboard_scores (board, account_id, points) VALUES ({weeklyBoard}, {account}, {(long)o.Weekly})
                    ON CONFLICT (board, account_id) DO UPDATE SET points = leaderboard_scores.points + EXCLUDED.points
                    """);
            await db.SaveChangesAsync();
        }
        await tx.CommitAsync();
        return true;
    }

    /// <summary>The season LP of an account now (ranked LP table and rankChange before/after).</summary>
    public async Task<(int Lp, int Shield)> RankAsync(Guid account)
    {
        await using var scope = scopes.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<GameDb>().Stats.AsNoTracking().Where(s => s.AccountId == account)
            .Select(s => new { s.Lp, s.LpSeason, s.Shield }).FirstOrDefaultAsync();
        if (row is null) return (0, 0);
        return row.LpSeason >= calendar.Season ? (row.Lp, row.Shield) : (Ranked.SeasonLp(row.Lp, row.LpSeason, calendar.Season), 0);
    }
}
