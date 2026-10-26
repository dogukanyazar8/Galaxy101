using System.Collections;
using System.Data.Common;
using System.Net.Http.Json;
using System.Reflection;
using Galaxy101.Contracts;
using Galaxy101.Server.Data;
using Galaxy101.Server.Economy;
using Galaxy101.Server.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Okey101.Core.V4;
using M = Galaxy101.Contracts.V4Messages;

namespace Galaxy101.Server.Tests.V4;

[Collection(ServerCollection.Name)]
public sealed class DurabilityRegressionTests(ServerFixture server)
{
    [Fact]
    public async Task Retrying_the_same_hand_XP_operation_applies_it_once()
    {
        var (client, tokens) = await server.GuestAsync();
        using (client)
        {
            var store = server.Services.GetRequiredService<Galaxy101.Server.V4.Store>();
            var key = $"v4:{Guid.CreateVersion7()}:xp:{tokens.AccountId:N}:1";
            await store.AddXpAsync(tokens.AccountId, 100, key);
            await store.AddXpAsync(tokens.AccountId, 100, key);
            await using var scope = server.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            Assert.Equal(100, await db.Stats.Where(s => s.AccountId == tokens.AccountId).Select(s => s.Xp).SingleAsync());
            Assert.Single(await db.Ledger.Where(l => l.AccountId == tokens.AccountId && l.IdempotencyKey == key + "#none").ToListAsync());
        }
    }

    [Fact]
    public async Task Lost_hand_XP_commit_acknowledgement_does_not_duplicate_the_retry()
    {
        var (client, tokens) = await server.GuestAsync();
        using (client)
        {
            var key = $"v4:{Guid.CreateVersion7()}:xp:{tokens.AccountId:N}:1";
            var (provider, faultingStore) = FaultingStore();
            await using var dispose = provider;
            await Assert.ThrowsAsync<InvalidOperationException>(() => faultingStore.AddXpAsync(tokens.AccountId, 100, key));
            await server.Services.GetRequiredService<Galaxy101.Server.V4.Store>().AddXpAsync(tokens.AccountId, 100, key);
            await using var scope = server.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            Assert.Equal(100, await db.Stats.Where(s => s.AccountId == tokens.AccountId).Select(s => s.Xp).SingleAsync());
            Assert.Single(await db.Ledger.Where(l => l.AccountId == tokens.AccountId && l.IdempotencyKey == key + "#none").ToListAsync());
        }
    }

    [Fact]
    public async Task A_failed_hand_XP_write_keeps_the_match_open_and_recovers_before_settlement()
    {
        await using var env = new V4Env(server);
        var red = Enumerable.Range(1, 13).Select(n => $"R{n}a").ToArray();
        var blue = Enumerable.Range(1, 8).Select(n => $"B{n}a").ToArray();
        env.Hub.SeedSource = () => 7;
        env.Hub.NextRig = G101Rules.RigFromHands(new ScenarioDeal
        {
            Dealer = 3, Dice = [4, 3], Indicator = "K4a", Racks = [[.. red, .. blue, "Y13a"]],
        });
        var c = await env.GuestAsync();
        Assert.True((await c.SendAsync(M.Hello)).Ok);
        await using var scope = server.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        await InstallFaultAsync(db, c.Account, "hand");
        try
        {
            Assert.True((await c.SendAsync(M.VsBotsStart, new { mode = "solo", hands = 1 })).Ok);
            Assert.True(await env.UntilAsync(() => c.Of(M.Turn).Count > 0));
            Assert.True((await c.SendAsync(M.GOpen, new { melds = new[] { new { kind = "run", tiles = red }, new { kind = "run", tiles = blue } } })).Ok);
            Assert.True((await c.SendAsync(M.GDiscard, new { tile = "Y13a" })).Ok);
            await env.SettleAsync();
            Assert.Empty(c.Of(M.MatchResult));
            Assert.Empty(c.Of(M.Xp));
            Assert.Equal(MatchStatus.Active, (await db.Matches.AsNoTracking().SingleAsync(m => m.Seats.Contains(c.Account))).Status);
        }
        finally
        {
            await ClearFaultAsync(db);
        }
        Assert.True(await env.UntilAsync(() => c.Of(M.MatchResult).Count > 0, 100));
        Assert.Equal(840, await db.Stats.Where(s => s.AccountId == c.Account).Select(s => s.Xp).SingleAsync());
        Assert.Equal(340, Assert.Single(c.Of(M.Xp), f => f.Str("reason") == "hand")["gained"].GetInt64());
        Assert.Equal(500, Assert.Single(c.Of(M.Xp), f => f.Str("reason") == "match")["gained"].GetInt64());
        Assert.Single(await db.Ledger.Where(l => l.AccountId == c.Account && l.Reason == "xp").ToListAsync());
    }

    [Fact]
    public async Task Settlement_receipt_survives_a_lost_commit_and_keeps_the_original_rank_and_XP()
    {
        var (client, tokens) = await server.GuestAsync();
        using (client)
        {
            var store = server.Services.GetRequiredService<Galaxy101.Server.V4.Store>();
            var calendar = server.Services.GetRequiredService<GameCalendar>();
            var table = Guid.CreateVersion7();
            await store.CreateRecordAsync(table, GameModes.Ranked, 0, 3, 15, new { engine = "v4" });
            await using var scope = server.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            await db.Stats.Where(s => s.AccountId == tokens.AccountId).ExecuteUpdateAsync(s => s
                .SetProperty(p => p.Lp, 99).SetProperty(p => p.LpSeason, calendar.Season).SetProperty(p => p.Shield, 1));
            await store.AddXpAsync(tokens.AccountId, 200);
            var outcome = Enumerable.Range(0, 4).Select(k => new Galaxy101.Server.V4.SeatSettlement(k, k == 0 ? tokens.AccountId : null,
                k + 1, 0, 0, 0, k == 0 ? 30 : null, 0, k == 0 ? 700 : 0,
                new QuestCounters(0, 0, 0, 0, 0), [], false, k == 0 ? 500 : 0)).ToArray();
            var receipt = new Galaxy101.Server.V4.SettlementReceipt(outcome, [99, 0, 0, 0], [1, 0, 0, 0], 0);
            Guid[] seats = [tokens.AccountId, Guid.Empty, Guid.Empty, Guid.Empty];
            var (provider, faultingStore) = FaultingStore();
            await using var dispose = provider;
            await Assert.ThrowsAsync<InvalidOperationException>(() => faultingStore.SettleAsync(table, GameModes.Ranked, seats, 14, outcome, receipt));

            var committed = Assert.IsType<Galaxy101.Server.V4.SettlementReceipt>(await store.SettlementAsync(table));
            Assert.Equal(99, committed.BeforeLp[0]);
            Assert.Equal(1, committed.BeforeShield[0]);
            Assert.Equal(30, committed.Outcome[0].LpDelta);
            Assert.Equal(500, committed.Outcome[0].BonusXp);
            Assert.NotNull(committed.Xp[0]);
            // A retry may have recomputed a different LP delta after the original promotion; the receipt wins.
            var changed = outcome.Select(o => o.Seat == 0 ? o with { LpDelta = 25, BonusXp = 1000 } : o).ToArray();
            Assert.False(await store.SettleAsync(table, GameModes.Ranked, seats, 14, changed));
            var reread = (await store.SettlementAsync(table))!;
            Assert.Equal(30, reread.Outcome[0].LpDelta);
            Assert.Equal(500, reread.Outcome[0].BonusXp);
            var stats = await db.Stats.AsNoTracking().SingleAsync(s => s.AccountId == tokens.AccountId);
            Assert.Equal(129, stats.Lp);
            Assert.Equal(0, stats.Shield);
            Assert.Equal(700, stats.Xp);
            Assert.Equal(4, await db.MatchParticipants.CountAsync(p => p.MatchId == table));
        }
    }

    [Fact]
    public async Task Retaking_the_same_seat_creates_a_distinct_leaver_participation()
    {
        await using var env = new V4Env(server);
        env.Hub.SeedSource = () => 42;
        var c = await env.GuestAsync();
        Assert.True((await c.SendAsync(M.Hello)).Ok);
        var created = await c.SendAsync(M.TableCreate, new { name = TableFlowTests.Unique("Retake seat"), mode = "solo", bet = 1000, hands = 3, turnSec = 15 });
        Assert.True(created.Ok);
        var tableId = Guid.Parse(created["table"].GetProperty("id").GetString()!);
        foreach (var seat in new[] { 1, 2, 3 }) Assert.True((await c.SendAsync(M.SeatAddBot, new { seat })).Ok);
        Assert.True((await c.SendAsync(M.TableStart)).Ok);
        Assert.True(await env.UntilAsync(() => c.Of(M.Turn).Count > 0));
        Assert.True((await c.SendAsync(M.TableLeave)).Ok);
        Assert.True((await c.SendAsync(M.TableJoin, new { tableId, seat = 0 })).Ok);
        Assert.True((await c.SendAsync(M.TableLeave)).Ok);
        await env.SettleAsync();
        await using var scope = server.Services.CreateAsyncScope();
        var rows = await scope.ServiceProvider.GetRequiredService<GameDb>().MatchParticipants.AsNoTracking()
            .Where(p => p.MatchId == tableId && p.AccountId == c.Account && p.Left).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, p => Assert.NotNull(p.OperationKey));
        Assert.Equal(2, rows.Select(p => p.OperationKey).Distinct().Count());
    }

    [Fact]
    public async Task Lost_leaver_commit_acknowledgement_does_not_duplicate_the_retry()
    {
        var (client, tokens) = await server.GuestAsync();
        using (client)
        {
            var store = server.Services.GetRequiredService<Galaxy101.Server.V4.Store>();
            var table = Guid.CreateVersion7();
            await store.CreateRecordAsync(table, GameModes.Solo, 0, 3, 15, new { engine = "v4" });
            var (provider, faultingStore) = FaultingStore();
            await using var dispose = provider;
            var counters = new QuestCounters(1, 0, 1, 0, 0);
            await Assert.ThrowsAsync<InvalidOperationException>(() => faultingStore.LeaverAsync(table, GameModes.Solo, 0, tokens.AccountId, 10, 0, counters, [true], null));
            await store.LeaverAsync(table, GameModes.Solo, 0, tokens.AccountId, 10, 0, counters, [true], null);
            await using var scope = server.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            Assert.Single(await db.MatchParticipants.Where(p => p.MatchId == table && p.AccountId == tokens.AccountId).ToListAsync());
            Assert.Equal(1, await db.Stats.Where(s => s.AccountId == tokens.AccountId).Select(s => s.Hands).SingleAsync());
        }
    }

    [Fact]
    public async Task Owed_waiting_room_refund_keeps_the_match_recoverable_until_the_refund_succeeds()
    {
        await using var env = new V4Env(server);
        env.Hub.SeedSource = () => 42;
        var owner = await env.GuestAsync();
        var leaver = await env.GuestAsync();
        foreach (var c in new[] { owner, leaver }) Assert.True((await c.SendAsync(M.Hello)).Ok);
        var created = await owner.SendAsync(M.TableCreate, new { name = TableFlowTests.Unique("Owed refund"), mode = "solo", bet = 1000, hands = 3, turnSec = 15 });
        Assert.True(created.Ok);
        var tableId = Guid.Parse(created["table"].GetProperty("id").GetString()!);
        Assert.True((await leaver.SendAsync(M.TableJoin, new { tableId })).Ok);
        await using var scope = server.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        await InstallFaultAsync(db, leaver.Account, "refund");
        try
        {
            Assert.True((await leaver.SendAsync(M.TableLeave)).Ok);
            foreach (var seat in new[] { 1, 2, 3 }) Assert.True((await owner.SendAsync(M.SeatAddBot, new { seat })).Ok);
            Assert.True((await owner.SendAsync(M.TableStart)).Ok);
            Assert.True(await env.UntilAsync(() => owner.Game("handEnd").Count == 3, 12000, 1000));
            await env.SettleAsync();
            Assert.Empty(owner.Of(M.MatchResult));
            Assert.Equal(MatchStatus.Active, (await db.Matches.AsNoTracking().SingleAsync(m => m.Id == tableId)).Status);
        }
        finally
        {
            await ClearFaultAsync(db);
        }
        Assert.True(await env.UntilAsync(() => owner.Of(M.MatchResult).Count > 0, 200, 1000));
        Assert.Equal(10000, await db.Wallets.Where(w => w.AccountId == leaver.Account).Select(w => w.Chips).SingleAsync());
        Assert.Equal(MatchStatus.Completed, (await db.Matches.AsNoTracking().SingleAsync(m => m.Id == tableId)).Status);
    }

    [Fact]
    public async Task Match_bonus_XP_failure_rolls_back_the_settlement_then_retries_once()
    {
        await using var env = new V4Env(server);
        var red = Enumerable.Range(1, 13).Select(n => $"R{n}a").ToArray();
        var blue = Enumerable.Range(1, 8).Select(n => $"B{n}a").ToArray();
        env.Hub.SeedSource = () => 7;
        env.Hub.NextRig = G101Rules.RigFromHands(new ScenarioDeal
        {
            Dealer = 3, Dice = [4, 3], Indicator = "K4a", Racks = [[.. red, .. blue, "Y13a"]],
        });
        var c = await env.GuestAsync();
        Assert.True((await c.SendAsync(M.Hello)).Ok);
        await using var scope = server.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        await InstallFaultAsync(db, c.Account, "bonus");
        try
        {
            Assert.True((await c.SendAsync(M.VsBotsStart, new { mode = "solo", hands = 1 })).Ok);
            Assert.True(await env.UntilAsync(() => c.Of(M.Turn).Count > 0));
            Assert.True((await c.SendAsync(M.GOpen, new { melds = new[] { new { kind = "run", tiles = red }, new { kind = "run", tiles = blue } } })).Ok);
            Assert.True((await c.SendAsync(M.GDiscard, new { tile = "Y13a" })).Ok);
            await env.SettleAsync();
            Assert.Empty(c.Of(M.MatchResult));
            var match = await db.Matches.AsNoTracking().SingleAsync(m => m.Seats.Contains(c.Account));
            Assert.Equal(MatchStatus.Active, match.Status);
        }
        finally
        {
            await ClearFaultAsync(db);
        }
        Assert.True(await env.UntilAsync(() => c.Of(M.MatchResult).Count > 0, 100));
        var participant = await db.MatchParticipants.AsNoTracking().SingleAsync(p => p.AccountId == c.Account && !p.Left);
        var stats = await db.Stats.AsNoTracking().SingleAsync(p => p.AccountId == c.Account);
        Assert.Equal(participant.Xp, stats.Xp);
        Assert.Single(c.Of(M.Xp), f => f.Str("reason") == "match");
    }

    [Fact]
    public async Task Retrying_committed_leaver_persistence_does_not_apply_it_twice()
    {
        var (client, tokens) = await server.GuestAsync();
        using (client)
        {
            var store = server.Services.GetRequiredService<Galaxy101.Server.V4.Store>();
            var table = Guid.CreateVersion7();
            await store.CreateRecordAsync(table, GameModes.Solo, 0, 3, 15, new { engine = "v4" });
            var counters = new QuestCounters(1, 0, 1, 0, 0);
            for (var attempt = 0; attempt < 2; attempt++)
                await store.LeaverAsync(table, GameModes.Solo, 0, tokens.AccountId, 10, 0, counters, [true], -20);
            await using var scope = server.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            Assert.Single(await db.MatchParticipants.Where(x => x.MatchId == table && x.AccountId == tokens.AccountId).ToListAsync());
            var stats = await db.Stats.AsNoTracking().SingleAsync(x => x.AccountId == tokens.AccountId);
            Assert.Equal(1, stats.Hands);
            Assert.Equal(1, stats.Wins);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Existing_blocks_filter_waiting_room_players_and_spectators(bool blockedSpectator, bool blockingSpectator)
    {
        await using var env = new V4Env(server);
        var owner = await env.GuestAsync();
        var sender = await env.GuestAsync();
        var blocker = await env.GuestAsync();
        foreach (var c in new[] { owner, sender, blocker }) Assert.True((await c.SendAsync(M.Hello)).Ok);
        (await blocker.Http.PostAsync($"/v1/players/{sender.Account}/block", null)).EnsureSuccessStatusCode();
        var created = await owner.SendAsync(M.TableCreate, new { name = TableFlowTests.Unique("Block filter"), mode = "solo", bet = 1000, hands = 3, turnSec = 15 });
        Assert.True(created.Ok);
        var tableId = created["table"].GetProperty("id").GetString();
        Assert.True((await sender.SendAsync(blockedSpectator ? M.TableSpectate : M.TableJoin, new { tableId })).Ok);
        Assert.True((await blocker.SendAsync(blockingSpectator ? M.TableSpectate : M.TableJoin, new { tableId })).Ok);

        Assert.True((await sender.SendAsync(M.ChatSend, new { text = "Merhaba" })).Ok);
        await env.SettleAsync();

        Assert.Contains(owner.Of(M.Chat), f => f.Str("user") == sender.Account.ToString());
        Assert.DoesNotContain(blocker.Of(M.Chat), f => f.Str("user") == sender.Account.ToString());
    }

    [Fact]
    public async Task Offline_people_are_released_after_their_match_finishes()
    {
        await using var env = new V4Env(server);
        env.Hub.SeedSource = () => 42;
        var c = await env.GuestAsync();
        Assert.True((await c.SendAsync(M.Hello)).Ok);
        Assert.True((await c.SendAsync(M.VsBotsStart, new { mode = "solo", hands = 1 })).Ok);
        await env.DropAsync(c);
        for (var step = 0; step < 4000 && People(env.Hub).Contains(c.Account); step++) await env.AdvanceAsync(1000);
        Assert.False(People(env.Hub).Contains(c.Account));
    }

    [Fact]
    public async Task Offline_people_are_released_after_the_ranked_accept_timeout()
    {
        await using var env = new V4Env(server);
        var players = new List<V4Client>();
        for (var i = 0; i < 4; i++)
        {
            var c = await env.GuestAsync();
            Assert.True((await c.SendAsync(M.Hello)).Ok);
            Assert.True((await c.SendAsync(M.RankedQueue, new { mode = "solo", party = Array.Empty<string>() })).Ok);
            players.Add(c);
        }
        var offline = players[0];
        await env.DropAsync(offline);
        await env.AdvanceAsync(10000);
        Assert.False(People(env.Hub).Contains(offline.Account));
    }

    private static IDictionary People(Galaxy101.Server.V4.Hub hub) =>
        (IDictionary)typeof(Galaxy101.Server.V4.Hub).GetField("people", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(hub)!;

    private static async Task InstallFaultAsync(GameDb db, Guid account, string kind)
    {
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE v4_test_faults (account_id uuid NOT NULL, kind text NOT NULL);
            CREATE FUNCTION v4_test_fail_bonus() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.xp - OLD.xp = 340 AND EXISTS (SELECT 1 FROM v4_test_faults WHERE account_id = NEW.account_id AND kind = 'hand') THEN
                    RAISE EXCEPTION 'injected hand XP failure';
                END IF;
                IF NEW.xp > OLD.xp AND EXISTS (SELECT 1 FROM v4_test_faults WHERE account_id = NEW.account_id AND kind = 'bonus')
                    AND EXISTS (SELECT 1 FROM matches WHERE status = 'Completed' AND NEW.account_id = ANY(seats)) THEN
                    RAISE EXCEPTION 'injected match bonus XP failure';
                END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER v4_test_bonus BEFORE UPDATE ON stats FOR EACH ROW EXECUTE FUNCTION v4_test_fail_bonus();
            CREATE FUNCTION v4_test_fail_refund() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW.reason = 'refund' AND EXISTS (SELECT 1 FROM v4_test_faults WHERE account_id = NEW.account_id AND kind = 'refund') THEN
                    RAISE EXCEPTION 'injected stake refund failure';
                END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER v4_test_refund BEFORE INSERT ON ledger FOR EACH ROW EXECUTE FUNCTION v4_test_fail_refund();
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO v4_test_faults (account_id, kind) VALUES ({account}, {kind})");
    }

    private static Task ClearFaultAsync(GameDb db) => db.Database.ExecuteSqlRawAsync("""
        DROP TRIGGER v4_test_bonus ON stats;
        DROP TRIGGER v4_test_refund ON ledger;
        DROP FUNCTION v4_test_fail_bonus();
        DROP FUNCTION v4_test_fail_refund();
        DROP TABLE v4_test_faults;
        """);

    private (ServiceProvider Provider, Galaxy101.Server.V4.Store Store) FaultingStore()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(server.Clock);
        services.AddSingleton(server.Services.GetRequiredService<GameCalendar>());
        services.AddSingleton(server.Services.GetRequiredService<Catalog>());
        services.AddDbContext<GameDb>(options => options.UseNpgsql(server.DatabaseServer).UseSnakeCaseNamingConvention().AddInterceptors(new LostCommitAcknowledgement()));
        services.AddScoped<Ledger>();
        services.AddScoped<DailyService>();
        var provider = services.BuildServiceProvider();
        return (provider, new Galaxy101.Server.V4.Store(provider.GetRequiredService<IServiceScopeFactory>(), provider.GetRequiredService<GameCalendar>()));
    }

    private sealed class LostCommitAcknowledgement : DbTransactionInterceptor
    {
        private int failures;
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref failures) == 1) throw new InvalidOperationException("Injected lost commit acknowledgement");
            return Task.CompletedTask;
        }
    }
}
