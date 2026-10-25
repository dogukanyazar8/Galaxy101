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
public sealed class MatchStore(IServiceScopeFactory scopes, GameCalendar calendar)
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
                if ((match.BotMask & (1 << s)) == 0 && (await ledger.ApplyAsync(match.Seats[s], -match.Stake, 0, "stake", $"match:{match.Id}:stake", ct)).Outcome == LedgerOutcome.InsufficientFunds)
                    unpaid.Add(s);
        if (unpaid.Count > 0) return [.. unpaid];
        await tx.CommitAsync(ct);
        return [];
    }

    // A write whose answer was lost (the row was stored, the call still failed) is retried by the session, which only
    // applies a hand or move once its write returns: the stored row never counted and nobody saw it, so the retry writes
    // over it instead of failing on the key for ever.

    public async Task StartHandAsync(Guid match, int hand, long seed, Guid roundId)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var written = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO match_hands (match_id, hand_index, seed, round_id, scores) VALUES ({match}, {hand}, {seed}, {roundId}, ARRAY[]::integer[])
            ON CONFLICT (match_id, hand_index) DO UPDATE SET seed = EXCLUDED.seed, round_id = EXCLUDED.round_id
            WHERE NOT EXISTS (SELECT 1 FROM match_moves m WHERE m.match_id = match_hands.match_id AND m.hand_index = match_hands.hand_index)
            """);
        if (written == 0) throw new InvalidOperationException($"Match {match} hand {hand} is already being played.");
    }

    public async Task AppendMoveAsync(Guid match, int hand, long version, int seat, string payload)
    {
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<GameDb>().Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO match_moves (match_id, hand_index, version, seat, payload) VALUES ({match}, {hand}, {version}, {seat}, {payload}::jsonb)
            ON CONFLICT (match_id, hand_index, version) DO UPDATE SET seat = EXCLUDED.seat, payload = EXCLUDED.payload
            """);
    }

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

    /// <summary>
    /// "Oyuna katıl": the newcomer's stake, the forfeit of the player who left the seat (if one did) and the seat's new
    /// owner, together or not at all. Null when done, else the error code.
    /// </summary>
    public async Task<string?> TakeSeatAsync(MatchRecord match, int seat, Guid account, Guid? leaver, int leaverTotal, int handIndex)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var ledger = scope.ServiceProvider.GetRequiredService<Ledger>();
        await using var tx = await db.Database.BeginTransactionAsync();
        if (match.Stake > 0)
        {
            var paid = await ledger.ApplyAsync(account, -match.Stake, 0, "stake", $"match:{match.Id}:join:{seat}:{handIndex}", default);
            if (paid.Outcome == LedgerOutcome.InsufficientFunds) return ErrorCodes.InsufficientFunds;
            if (paid.Outcome == LedgerOutcome.Duplicate) return ErrorCodes.Conflict;
        }
        if (leaver is { } gone)
            db.MatchParticipants.Add(new MatchParticipant
            {
                MatchId = match.Id, Seat = seat, AccountId = await db.Accounts.AnyAsync(a => a.Id == gone) ? gone : null, Mode = match.Mode,
                Place = 4, Total = leaverTotal, ChipDelta = -match.Stake, Left = true, FinishedAt = calendar.Now,
            });
        var seats = match.Seats.ToArray(); seats[seat] = account;
        var keep = ~(1 << seat);
        await db.Matches.Where(m => m.Id == match.Id).ExecuteUpdateAsync(s => s
            .SetProperty(m => m.Seats, seats).SetProperty(m => m.BotMask, m => m.BotMask & keep).SetProperty(m => m.LeftMask, m => m.LeftMask & keep));
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return null;
    }

    /// <summary>Each seat's LP in the current season (0 for bots and accounts without stats).</summary>
    public async Task<int[]> SeasonLpAsync(IReadOnlyList<Guid> seats)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var rows = await db.Stats.AsNoTracking().Where(x => seats.Contains(x.AccountId)).Select(x => new { x.AccountId, x.Lp, x.LpSeason }).ToListAsync();
        var season = calendar.Season;
        return [.. seats.Select(id => rows.FirstOrDefault(r => r.AccountId == id) is { } r ? Profile.Ranked.SeasonLp(r.Lp, r.LpSeason, season) : 0)];
    }

    public async Task<HashSet<(Guid, Guid)>> BlocksAsync(IReadOnlyCollection<Guid> accounts)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        var rows = await db.Blocks.AsNoTracking().Where(b => accounts.Contains(b.BlockerId) && accounts.Contains(b.BlockedId))
            .Select(b => new { b.BlockerId, b.BlockedId }).ToListAsync();
        return [.. rows.Select(b => (b.BlockerId, b.BlockedId))];
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
        var season = calendar.Season;
        var seasonBoard = Social.LeaderboardEndpoints.SeasonBoard(season);
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

            // Row locks in one order everywhere (daily state, stats, wallet): the daily claims lock the state before paying
            // and a season roll-over locks the stats before its reward, so settling in another order could deadlock.
            await daily.AddProgressAsync(account, t.Counters, default);
            var stats = (await db.Stats.FromSql($"SELECT * FROM stats WHERE account_id = {account} FOR UPDATE").ToListAsync()).Single();
            await Profile.Ranked.RollOverAsync(db, ledger, stats, season, default);
            if (o.Payout > 0) await ledger.ApplyAsync(account, o.Payout, 0, "payout", $"match:{match.Id}:payout", default);
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
                (stats.Lp, stats.Shield) = Profile.Ranked.Apply(stats.Lp, stats.Shield, lp);
                stats.BestLp = Math.Max(stats.BestLp, stats.Lp);
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
        var ids = matches.Select(m => m.Id).ToList();
        // Three queries in all (not two per match), grouped in memory.
        var hands = (await db.MatchHands.AsNoTracking().Where(h => ids.Contains(h.MatchId)).OrderBy(h => h.HandIndex).ToListAsync(ct)).ToLookup(h => h.MatchId);
        var moves = (await db.MatchMoves.AsNoTracking().Where(x => ids.Contains(x.MatchId)).OrderBy(x => x.HandIndex).ThenBy(x => x.Version).ToListAsync(ct))
            .ToLookup(x => (x.MatchId, x.HandIndex));
        var result = new List<StoredMatch>();
        foreach (var m in matches)
            result.Add(new StoredMatch(m, [.. hands[m.Id].Select(h => new StoredHand(h.HandIndex, h.Seed, h.RoundId, h.Scores, [.. moves[(m.Id, h.HandIndex)]]))]));
        return result;
    }
}
