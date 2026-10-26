using System.Net.Http.Json;
using Galaxy101.Contracts;
using Galaxy101.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Okey101.Core.V4;
using M = Galaxy101.Contracts.V4Messages;

namespace Galaxy101.Server.Tests.V4;

/// <summary>Money and counters around the v4 tables: what a restart returns, what an undo takes back, what a block changes.</summary>
[Collection(ServerCollection.Name)]
public sealed class AuditFixTests(ServerFixture server)
{
    [Fact]
    public async Task A_restart_returns_the_stakes_a_table_holds_but_not_the_gifts_sent_there()
    {
        await using var env = new V4Env(server);
        var c = await env.GuestAsync();
        var store = server.Services.GetRequiredService<Galaxy101.Server.V4.Store>();
        var start = await store.ChipsAsync(c.Account);
        var table = Guid.CreateVersion7();
        await store.CreateRecordAsync(table, GameModes.Solo, 1000, 3, 15, new { engine = Galaxy101.Server.V4.Store.Engine });
        await store.ApplyAsync(c.Account, -1000, "stake", $"v4:{table}:bet:{c.Account:N}:1");
        await store.ApplyAsync(c.Account, -500, "gift", $"v4:{table}:gift:{c.Account:N}:2");
        await store.AbortOpenAsync(default);
        Assert.Equal(start - 500, await store.ChipsAsync(c.Account));
        await using var scope = server.Services.CreateAsyncScope();
        var record = await scope.ServiceProvider.GetRequiredService<GameDb>().Matches.AsNoTracking().SingleAsync(m => m.Id == table);
        Assert.Equal(MatchStatus.Aborted, record.Status);
    }

    [Fact]
    public async Task A_stake_is_returned_only_once_and_only_if_it_was_taken()
    {
        await using var env = new V4Env(server);
        var c = await env.GuestAsync();
        var store = server.Services.GetRequiredService<Galaxy101.Server.V4.Store>();
        var start = await store.ChipsAsync(c.Account);
        var key = $"v4:{Guid.CreateVersion7()}:bet:{c.Account:N}:1";
        Assert.Null(await store.RefundStakeAsync(c.Account, 1000, key));
        await store.ApplyAsync(c.Account, -1000, "stake", key);
        Assert.Equal(Galaxy101.Server.Economy.LedgerOutcome.Applied, (await store.RefundStakeAsync(c.Account, 1000, key))!.Value.Outcome);
        Assert.Equal(Galaxy101.Server.Economy.LedgerOutcome.Duplicate, (await store.RefundStakeAsync(c.Account, 1000, key))!.Value.Outcome);
        Assert.Equal(start, await store.ChipsAsync(c.Account));
    }

    [Fact]
    public async Task An_undone_pairs_opening_does_not_count_for_the_daily_quest()
    {
        await using var env = new V4Env(server);
        var racks = new List<List<string>>
        {
            new() { "R1a", "R1b", "R2a", "R2b", "R3a", "R3b", "R4a", "R4b", "R5a", "R5b", "B1a", "B3a", "B5a", "B7a", "B9a", "B11a", "Y2a", "Y4a", "Y6a", "Y8a", "Y10a", "Y12a" },
            new() { "K1a", "K2a", "K3a", "K6a", "K7a", "K8a", "K9a", "K10a", "K11a", "K12a", "K13a", "B2a", "B4a", "B6a", "B8a", "B10a", "B12a", "B13a", "Y1a", "Y3a", "Y5a" },
        };
        env.Hub.SeedSource = () => 7;
        env.Hub.NextRig = G101Rules.RigFromHands(new ScenarioDeal { Dealer = 3, Dice = [4, 3], Indicator = "K4a", Racks = racks });
        var c = await env.GuestAsync();
        await c.SendAsync(M.Hello);
        Assert.True((await c.SendAsync(M.VsBotsStart, new { mode = "solo", hands = 1 })).Ok);
        Assert.True(await env.UntilAsync(() => c.Of(M.Turn).Count > 0));
        var pairs = Enumerable.Range(1, 5).Select(n => new { kind = "pair", tiles = new[] { $"R{n}a", $"R{n}b" } }).ToArray();
        for (var i = 0; i < 2; i++)
        {
            Assert.True((await c.SendAsync(M.GOpen, new { melds = pairs })).Ok);
            Assert.True((await c.SendAsync(M.GUndo)).Ok);
        }
        Assert.True((await c.SendAsync(M.GDiscard, new { tile = "Y12a" })).Ok);
        Assert.True((await c.SendAsync(M.TableLeave)).Ok);
        await env.SettleAsync();
        var daily = (await c.Http.GetFromJsonAsync<DailyResponse>("/v1/daily"))!;
        Assert.Equal(0, daily.Quests.Single(q => q.Id == "pairsOpenings").Progress);
    }
}
