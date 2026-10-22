using Galaxy101.Contracts;
using Galaxy101.Server.Data;
using Galaxy101.Server.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Galaxy101.Server.Economy;

/// <summary>Counters a finished online hand contributes to the daily quests, keyed by quest id.</summary>
public sealed record QuestCounters(int WinHands, int PairsOpenings, int HandsPlayed, int OkeyFinishes, int TilesAdded)
{
    public int For(string questId) => questId switch
    {
        "winHands" => WinHands, "pairsOpenings" => PairsOpenings, "handsPlayed" => HandsPlayed,
        "okeyFinishes" => OkeyFinishes, "tilesAdded" => TilesAdded, _ => 0,
    };
}

/// <summary>
/// Daily streak and quests. "Today" is the server's calendar day (Europe/Istanbul); the device clock is never consulted.
/// Quest progress resets lazily the first time an account is touched on a new day.
/// </summary>
public sealed class DailyService(GameDb db, Ledger ledger, Catalog catalog, GameCalendar calendar)
{
    public async Task<DailyResponse> DescribeAsync(Guid account, CancellationToken ct)
    {
        var state = await CurrentAsync(account, ct);
        var today = calendar.Today;
        var claimedToday = state.LastClaimDay == today;
        var alive = claimedToday || state.LastClaimDay == today.AddDays(-1);
        var done = alive ? state.StreakCount : 0;
        // After the seventh day the cycle restarts from day one.
        if (!claimedToday && done == 7) done = 0;
        var next = claimedToday ? 0 : done + 1;
        return new DailyResponse
        {
            Streak = [.. catalog.Streak.Select((chips, i) => new StreakDayResponse { Day = i + 1, Chips = chips, Claimed = i < done, Today = i + 1 == (claimedToday ? done : next) })],
            CanClaimStreak = !claimedToday,
            Quests = [.. catalog.Quests.Select((q, i) => new QuestResponse
            {
                Id = q.Id, Target = q.Target, Chips = q.Chips, Gems = q.Gems,
                Progress = Math.Min(Progress(state, i), q.Target), Complete = Progress(state, i) >= q.Target,
                Claimed = (state.QuestClaimedMask & (1 << i)) != 0,
            })],
            NextReset = calendar.NextReset,
            Wallet = await ledger.DescribeAsync(account, ct),
        };
    }

    public async Task<string?> ClaimStreakAsync(Guid account, CancellationToken ct)
    {
        var today = calendar.Today;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var state = await db.DailyStates.FromSql($"SELECT * FROM daily_states WHERE account_id = {account} FOR UPDATE").FirstOrDefaultAsync(ct);
        if (state is null) return ErrorCodes.NotFound;
        if (state.LastClaimDay == today) return ErrorCodes.AlreadyClaimed;
        var day = state.LastClaimDay == today.AddDays(-1) ? state.StreakCount % 7 + 1 : 1;
        var result = await ledger.ApplyAsync(account, catalog.Streak[day - 1], 0, "daily", $"daily:{today:yyyy-MM-dd}", ct);
        if (result.Outcome != LedgerOutcome.Applied) return ErrorCodes.AlreadyClaimed;
        state.StreakCount = day;
        state.LastClaimDay = today;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return null;
    }

    public async Task<string?> ClaimQuestAsync(Guid account, string questId, CancellationToken ct)
    {
        var index = catalog.Quests.ToList().FindIndex(x => x.Id == questId);
        if (index < 0) return ErrorCodes.NotFound;
        var quest = catalog.Quests[index];
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var state = await LockedCurrentAsync(account, ct);
        if (state is null) return ErrorCodes.NotFound;
        if ((state.QuestClaimedMask & (1 << index)) != 0) return ErrorCodes.AlreadyClaimed;
        if (Progress(state, index) < quest.Target) return ErrorCodes.NotComplete;
        var result = await ledger.ApplyAsync(account, quest.Chips, quest.Gems, "quest", $"quest:{state.QuestDay:yyyy-MM-dd}:{quest.Id}", ct);
        if (result.Outcome != LedgerOutcome.Applied) return ErrorCodes.AlreadyClaimed;
        state.QuestClaimedMask |= 1 << index;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return null;
    }

    /// <summary>Adds a finished hand's counters. Runs inside the caller's (match settlement) transaction.</summary>
    public async Task AddProgressAsync(Guid account, QuestCounters counters, CancellationToken ct)
    {
        var state = await LockedCurrentAsync(account, ct);
        if (state is null) return;
        var progress = state.QuestProgress.ToArray();
        for (var i = 0; i < catalog.Quests.Count; i++) progress[i] = (int)Math.Min(int.MaxValue, (long)progress[i] + counters.For(catalog.Quests[i].Id));
        state.QuestProgress = progress;
        await db.SaveChangesAsync(ct);
    }

    private async Task<DailyState> CurrentAsync(Guid account, CancellationToken ct)
    {
        var state = await db.DailyStates.AsNoTracking().FirstAsync(x => x.AccountId == account, ct);
        if (state.QuestDay != calendar.Today) { state.QuestDay = calendar.Today; state.QuestProgress = []; state.QuestClaimedMask = 0; }
        return state;
    }

    private async Task<DailyState?> LockedCurrentAsync(Guid account, CancellationToken ct)
    {
        var state = await db.DailyStates.FromSql($"SELECT * FROM daily_states WHERE account_id = {account} FOR UPDATE").FirstOrDefaultAsync(ct);
        if (state is null) return null;
        if (state.QuestDay != calendar.Today) { state.QuestDay = calendar.Today; state.QuestProgress = []; state.QuestClaimedMask = 0; }
        if (state.QuestProgress.Length < catalog.Quests.Count)
            state.QuestProgress = [.. state.QuestProgress, .. new int[catalog.Quests.Count - state.QuestProgress.Length]];
        return state;
    }

    private static int Progress(DailyState state, int index) => index < state.QuestProgress.Length ? state.QuestProgress[index] : 0;
}

public static class DailyEndpoints
{
    public static void MapDaily(this IEndpointRouteBuilder app)
    {
        var daily = app.MapGroup("/v1/daily").RequireAuthorization();

        daily.MapGet("", async (HttpContext http, DailyService service, CancellationToken ct) =>
            Results.Ok(await service.DescribeAsync(http.User.AccountId(), ct)));

        daily.MapPost("/claim", async (HttpContext http, DailyService service, CancellationToken ct) =>
            await Respond(http, service, await service.ClaimStreakAsync(http.User.AccountId(), ct), ct));

        daily.MapPost("/quests/{id}/claim", async (string id, HttpContext http, DailyService service, CancellationToken ct) =>
            await Respond(http, service, await service.ClaimQuestAsync(http.User.AccountId(), id, ct), ct));
    }

    private static async Task<IResult> Respond(HttpContext http, DailyService service, string? error, CancellationToken ct) => error switch
    {
        null => Results.Ok(await service.DescribeAsync(http.User.AccountId(), ct)),
        ErrorCodes.NotFound => Api.NotFound(),
        _ => Api.Conflict(error),
    };
}
