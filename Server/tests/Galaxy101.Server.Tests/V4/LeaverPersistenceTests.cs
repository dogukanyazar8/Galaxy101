using Galaxy101.Contracts;
using Galaxy101.Server.Data;
using Galaxy101.Server.Economy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Okey101.Core.V4;
using M = Galaxy101.Contracts.V4Messages;

namespace Galaxy101.Server.Tests.V4;

[Collection(ServerCollection.Name)]
public sealed class LeaverPersistenceTests(ServerFixture server)
{
    [Fact]
    public async Task A_leaver_write_retries_beyond_five_failures_and_blocks_completion_until_it_succeeds()
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
        var watcher = await env.GuestAsync();
        foreach (var client in new[] { c, watcher }) Assert.True((await client.SendAsync(M.Hello)).Ok);
        var created = await c.SendAsync(M.TableCreate, new { name = TableFlowTests.Unique("Leaver retry"), mode = "solo", bet = 1000, hands = 3, turnSec = 15 });
        Assert.True(created.Ok);
        var tableId = Guid.Parse(created["table"].GetProperty("id").GetString()!);
        Assert.True((await watcher.SendAsync(M.TableSpectate, new { tableId })).Ok);
        foreach (var seat in new[] { 1, 2, 3 }) Assert.True((await c.SendAsync(M.SeatAddBot, new { seat })).Ok);
        Assert.True((await c.SendAsync(M.TableStart)).Ok);
        Assert.True(await env.UntilAsync(() => c.Of(M.Turn).Count > 0));
        Assert.True((await c.SendAsync(M.GOpen, new { melds = new[] { new { kind = "run", tiles = red }, new { kind = "run", tiles = blue } } })).Ok);
        Assert.True((await c.SendAsync(M.GDiscard, new { tile = "Y13a" })).Ok);

        await using var scope = server.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE v4_leaver_fault (account_id uuid PRIMARY KEY);
            CREATE SEQUENCE v4_leaver_failures;
            CREATE FUNCTION v4_fail_leaver() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN
                IF NEW."left" AND EXISTS (SELECT 1 FROM v4_leaver_fault WHERE account_id = NEW.account_id) THEN
                    PERFORM nextval('v4_leaver_failures');
                    RAISE EXCEPTION 'injected leaver persistence failure';
                END IF;
                RETURN NEW;
            END $$;
            CREATE TRIGGER v4_leaver_fault BEFORE INSERT ON match_participants FOR EACH ROW EXECUTE FUNCTION v4_fail_leaver();
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO v4_leaver_fault (account_id) VALUES ({c.Account})");
        try
        {
            Assert.True((await c.SendAsync(M.TableLeave)).Ok);
            await env.AdvanceAsync(180000);
            Assert.True(await env.UntilAsync(() => watcher.Game("handEnd").Count == 3, 12000, 1000));
            var attempts = await db.Database.SqlQueryRaw<long>("SELECT last_value AS \"Value\" FROM v4_leaver_failures").SingleAsync();
            Assert.True(attempts > 5, $"Only {attempts} attempts were made; a pending leaver must keep retrying.");
            Assert.Empty(watcher.Of(M.MatchResult));
            Assert.Equal(MatchStatus.Active, (await db.Matches.AsNoTracking().SingleAsync(m => m.Id == tableId)).Status);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("""
                DROP TRIGGER v4_leaver_fault ON match_participants;
                DROP FUNCTION v4_fail_leaver();
                DROP TABLE v4_leaver_fault;
                DROP SEQUENCE v4_leaver_failures;
                """);
        }
        Assert.True(await env.UntilAsync(() => watcher.Of(M.MatchResult).Count > 0, 200, 1000));
        var participant = Assert.Single(await db.MatchParticipants.Where(p => p.MatchId == tableId && p.AccountId == c.Account && p.Left).ToListAsync());
        Assert.NotNull(participant.OperationKey);
        var stats = await db.Stats.AsNoTracking().SingleAsync(s => s.AccountId == c.Account);
        Assert.Equal(1, stats.Hands);
        Assert.Equal(1, stats.Wins);
        Assert.Equal(MatchStatus.Completed, (await db.Matches.AsNoTracking().SingleAsync(m => m.Id == tableId)).Status);
    }

    [Fact]
    public async Task Retrying_a_deleted_leaver_records_one_anonymous_participation()
    {
        var (client, tokens) = await server.GuestAsync();
        using (client)
        {
            var store = server.Services.GetRequiredService<Galaxy101.Server.V4.Store>();
            var table = Guid.CreateVersion7();
            await store.CreateRecordAsync(table, GameModes.Solo, 0, 3, 15, new { engine = "v4" });
            (await client.DeleteAsync("/v1/account")).EnsureSuccessStatusCode();
            var counters = new QuestCounters(1, 0, 1, 0, 0);
            var key = $"v4:{table}:leaver:{Guid.NewGuid():N}";
            await store.LeaverAsync(table, GameModes.Solo, 0, tokens.AccountId, 10, 0, counters, [true], null, key);
            await store.LeaverAsync(table, GameModes.Solo, 0, tokens.AccountId, 10, 0, counters, [true], null, key);
            await using var scope = server.Services.CreateAsyncScope();
            var participant = Assert.Single(await scope.ServiceProvider.GetRequiredService<GameDb>().MatchParticipants.Where(p => p.MatchId == table).ToListAsync());
            Assert.Null(participant.AccountId);
            Assert.True(participant.Left);
        }
    }
}
