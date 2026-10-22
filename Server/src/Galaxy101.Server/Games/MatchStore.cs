using Galaxy101.Contracts;
using Galaxy101.Server.Data;
using Galaxy101.Server.Economy;
using Galaxy101.Server.Infrastructure;
using Galaxy101.Server.Social;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Galaxy101.Server.Games;

public sealed record StoredHand(int HandIndex, long Seed, Guid RoundId, int[] Scores, List<MatchMove> Moves);
public sealed record StoredMatch(MatchRecord Record, List<StoredHand> Hands);

/// <summary>Per-seat match aggregates handed to settlement.</summary>
public sealed record SeatTally(QuestCounters Counters, IReadOnlyList<bool> HandWins, long Xp, int BestOpening);

/// <summary>Durable side of online matches. Each call uses its own short-lived DbContext (sessions are long-lived singletons).</summary>
public sealed class MatchStore(IServiceScopeFactory scopes, GameCalendar calendar, IOptions<GameOptions> options)
{
    /// <summary>Creates the match and takes every stake in one transaction; returns the seats that could not pay.</summary>
    public async Task<int[]> CreateAsync(MatchRecord match, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var ledger = scope.ServiceProvider.GetRequiredService<Ledger>();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        db.Matches.Add(match);
        await db.SaveChangesAsync(ct);
        var unpaid = new List<int>();
        if (match.Stake > 0)
            for (var s = 0; s < 4; s++)
                if ((await ledger.ApplyAsync(match.Seats[s], -match.Stake, 0, "stake", $"match:{match.Id}:stake", ct)).Outcome == LedgerOutcome.InsufficientFunds)
                    unpaid.Add(s);
        if (unpaid.Count > 0) return [.. unpaid];
        await tx.CommitAsync(ct);
        return [];
    }

    public Task StartHandAsync(Guid match, int hand, long seed, Guid roundId) => Write(db =>
        db.MatchHands.Add(new MatchHand { MatchId = match, HandIndex = hand, Seed = seed, RoundId = roundId }));

    public Task AppendMoveAsync(Guid match, int hand, long version, int seat, string payload) => Write(db =>
        db.MatchMoves.Add(new MatchMove { MatchId = match, HandIndex = hand, Version = version, Seat = seat, Payload = payload }));

    public async Task FinishHandAsync(Guid match, int hand, int[] scores)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<GameDb>().MatchHands.Where(h => h.MatchId == match && h.HandIndex == hand)
            .ExecuteUpdateAsync(s => s.SetProperty(h => h.Scores, scores));
    }

    public async Task SetLeftAsync(Guid match, int leftMask)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<GameDb>().Matches.Where(m => m.Id == match)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.LeftMask, leftMask));
    }

    /// <summary>Charges a table gift; returns the new balance or null when the sender cannot afford it.</summary>
    public async Task<long?> ChargeAsync(Guid account, long chips, string reason)
    {
        await using var scope = scopes.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<Ledger>().ApplyAsync(account, -chips, 0, reason, $"{reason}:{Guid.NewGuid():N}", default);
        return result.Outcome == LedgerOutcome.Applied ? result.Chips : null;
    }

    public async Task<WalletResponse> WalletAsync(Guid account)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<Ledger>().DescribeAsync(account, default);
    }

    public async Task<Dictionary<Guid, PlayerSummary>> SummariesAsync(IReadOnlyCollection<Guid> accounts)
    {
        await using var scope = scopes.CreateAsyncScope();
        return await Players.SummariesAsync(scope.ServiceProvider.GetRequiredService<GameDb>(), accounts, default);
    }

    /// <summary>Pays out, updates stats, leaderboards and quests, records participants and closes the match: all or nothing.</summary>
    public async Task SettleAsync(MatchRecord match, SeatOutcome[] outcomes, SeatTally[] tallies, int[] totals)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var ledger = scope.ServiceProvider.GetRequiredService<Ledger>();
        var daily = scope.ServiceProvider.GetRequiredService<DailyService>();
        var now = calendar.Now;
        var modeIndex = Array.IndexOf(GameModes.Rated, match.Mode);
        var weeklyBoard = calendar.WeeklyBoard;
        var seasonBoard = Social.LeaderboardEndpoints.SeasonBoard(options.Value.Season);
        var existing = (await db.Accounts.Where(a => match.Seats.Contains(a.Id)).Select(a => a.Id).ToListAsync()).ToHashSet();

        await using var tx = await db.Database.BeginTransactionAsync();
        for (var s = 0; s < 4; s++)
        {
            var account = match.Seats[s];
            var o = outcomes[s];
            var t = tallies[s];
            var present = existing.Contains(account);
            db.MatchParticipants.Add(new MatchParticipant
            {
                MatchId = match.Id, Seat = s, AccountId = present ? account : null, Mode = match.Mode, Place = o.Place, Total = totals[s],
                ChipDelta = o.ChipDelta, LpDelta = o.LpDelta, Xp = t.Xp, Left = (match.LeftMask & (1 << s)) != 0, FinishedAt = now,
            });
            if (!present) continue; // deleted mid-match

            if (o.Payout > 0) await ledger.ApplyAsync(account, o.Payout, 0, "payout", $"match:{match.Id}:payout", default);
            var stats = (await db.Stats.FromSql($"SELECT * FROM stats WHERE account_id = {account} FOR UPDATE").ToListAsync()).Single();
            stats.Xp += t.Xp;
            stats.Hands += t.Counters.HandsPlayed;
            stats.Wins += t.Counters.WinHands;
            stats.OkeyFinishes += t.Counters.OkeyFinishes;
            stats.PairsOpenings += t.Counters.PairsOpenings;
            stats.BestOpening = Math.Max(stats.BestOpening, t.BestOpening);
            foreach (var won in t.HandWins)
            {
                stats.CurrentStreak = won ? stats.CurrentStreak + 1 : 0;
                stats.BestStreak = Math.Max(stats.BestStreak, stats.CurrentStreak);
            }
            if (modeIndex >= 0)
            {
                stats.ModeHands = [.. stats.ModeHands.Select((v, i) => i == modeIndex ? v + t.Counters.HandsPlayed : v)];
                stats.ModeWins = [.. stats.ModeWins.Select((v, i) => i == modeIndex ? v + t.Counters.WinHands : v)];
            }
            if (o.LpDelta is { } lp)
            {
                stats.Lp = Math.Max(0, stats.Lp + lp);
                await db.Database.ExecuteSqlAsync($"""
                    INSERT INTO leaderboard_scores (board, account_id, points) VALUES ({seasonBoard}, {account}, {(long)stats.Lp})
                    ON CONFLICT (board, account_id) DO UPDATE SET points = EXCLUDED.points
                    """);
            }
            if (o.WeeklyPoints > 0)
                await db.Database.ExecuteSqlAsync($"""
                    INSERT INTO leaderboard_scores (board, account_id, points) VALUES ({weeklyBoard}, {account}, {(long)o.WeeklyPoints})
                    ON CONFLICT (board, account_id) DO UPDATE SET points = leaderboard_scores.points + EXCLUDED.points
                    """);
            await db.SaveChangesAsync();
            await daily.AddProgressAsync(account, t.Counters, default);
        }
        match.Status = MatchStatus.Completed;
        match.FinishedAt = now;
        db.Matches.Update(match);
        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    /// <summary>Gives every stake back and closes the match (a round that cannot be restored, or a failed start).</summary>
    public async Task AbortAsync(MatchRecord match)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var ledger = scope.ServiceProvider.GetRequiredService<Ledger>();
        await using var tx = await db.Database.BeginTransactionAsync();
        if (match.Stake > 0)
            foreach (var account in match.Seats)
                if (await db.Ledger.AnyAsync(l => l.AccountId == account && l.IdempotencyKey == $"match:{match.Id}:stake#chips"))
                    await ledger.ApplyAsync(account, match.Stake, 0, "refund", $"match:{match.Id}:refund", default);
        await db.Matches.Where(m => m.Id == match.Id).ExecuteUpdateAsync(s => s.SetProperty(m => m.Status, MatchStatus.Aborted).SetProperty(m => m.FinishedAt, calendar.Now));
        await tx.CommitAsync();
    }

    public async Task<List<StoredMatch>> LoadActiveAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var matches = await db.Matches.AsNoTracking().Where(m => m.Status == MatchStatus.Active).ToListAsync(ct);
        var result = new List<StoredMatch>();
        foreach (var m in matches)
        {
            var hands = await db.MatchHands.AsNoTracking().Where(h => h.MatchId == m.Id).OrderBy(h => h.HandIndex).ToListAsync(ct);
            var moves = await db.MatchMoves.AsNoTracking().Where(x => x.MatchId == m.Id).OrderBy(x => x.HandIndex).ThenBy(x => x.Version).ToListAsync(ct);
            result.Add(new StoredMatch(m, [.. hands.Select(h => new StoredHand(h.HandIndex, h.Seed, h.RoundId, h.Scores, [.. moves.Where(x => x.HandIndex == h.HandIndex)]))]));
        }
        return result;
    }

    private async Task Write(Action<GameDb> add)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        add(db);
        await db.SaveChangesAsync();
    }
}
