using Galaxy101.Contracts;
using Galaxy101.Server.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using M = Galaxy101.Contracts.V4Messages;

namespace Galaxy101.Server.Tests.V4;

[Collection(ServerCollection.Name)]
public sealed class RankedChatBlockTests(ServerFixture server)
{
    [Fact]
    public async Task A_failed_rank_read_preserves_the_ranked_seat_until_leave_can_be_retried()
    {
        await using var env = new V4Env(server);
        var (players, matchId) = await ReadyThreeAsync(env);
        var c = players[0];
        Assert.True((await players[3].SendAsync(M.RankedAccept, new { matchId, accept = true })).Ok);
        Assert.True(await env.UntilAsync(() => c.Of(M.Turn).Count > 0));
        var tableId = Guid.Parse(c.Last(M.RankedReady)!["table"].GetProperty("id").GetString()!);
        await using var scope = server.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE stats RENAME TO v4_test_stats_unavailable");
        try
        {
            var leave = await c.SendAsync(M.TableLeave);
            Assert.False(leave.Ok);
            Assert.Equal("server_error", leave.Code);
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE v4_test_stats_unavailable RENAME TO stats");
        }
        Assert.True((await c.SendAsync(M.Resync)).Ok);
        Assert.Equal("table", c.Last(M.Snapshot)!.Str("screen"));
        Assert.Equal(tableId.ToString(), c.Last(M.Snapshot)!["table"].GetProperty("id").GetString());
        Assert.Empty(c.Of(M.RankChange));
        Assert.True((await c.SendAsync(M.TableLeave)).Ok);
        await env.SettleAsync();
        Assert.Single(c.Of(M.RankChange));
        var participation = Assert.Single(await db.MatchParticipants.Where(p => p.MatchId == tableId && p.AccountId == c.Account).ToListAsync());
        Assert.True(participation.Left);
        Assert.Equal(-20, participation.LpDelta);
        foreach (var player in players.Skip(1)) await player.SendAsync(M.TableLeave);
    }

    [Fact]
    public async Task Ranked_loading_chat_obeys_blocks_that_predate_the_match()
    {
        await using var env = new V4Env(server);
        var (players, matchId) = await ReadyThreeAsync(env);
        Assert.True((await players[3].SendAsync(M.RankedAccept, new { matchId, accept = true })).Ok);
        Assert.True((await players[1].SendAsync(M.ChatSend, new { quick = "quick.0" })).Ok);
        await env.SettleAsync();

        Assert.Empty(players[0].Of(M.Chat));
        Assert.Single(players[2].Of(M.Chat));
        Assert.Single(players[0].Of(M.RankedReady));
        foreach (var player in players) await player.SendAsync(M.TableLeave);
    }

    [Fact]
    public async Task Ranked_loading_chat_rejects_messages_while_blocks_are_unavailable_then_recovers()
    {
        await using var env = new V4Env(server);
        var (players, matchId) = await ReadyThreeAsync(env);
        await using var scope = server.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDb>();
        // This isolated fixture database has no SELECT triggers. A reversible rename injects a real read failure.
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE blocks RENAME TO v4_test_blocks_unavailable");
        try
        {
            Assert.True((await players[3].SendAsync(M.RankedAccept, new { matchId, accept = true })).Ok);
            var chat = await players[1].SendAsync(M.ChatSend, new { quick = "quick.0" });
            Assert.False(chat.Ok);
            Assert.Equal("server_error", chat.Code);
            await env.SettleAsync();
            Assert.All(players, p => Assert.Empty(p.Of(M.Chat)));
            Assert.All(players, p => Assert.Single(p.Of(M.RankedReady)));
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE v4_test_blocks_unavailable RENAME TO blocks");
        }
        await env.AdvanceAsync(5000);
        Assert.All(players, p => Assert.Single(p.Of(M.MatchStart)));
        Assert.True((await players[1].SendAsync(M.ChatSend, new { quick = "quick.0" })).Ok);
        await env.SettleAsync();
        Assert.Empty(players[0].Of(M.Chat));
        Assert.Single(players[2].Of(M.Chat));
        foreach (var player in players) await player.SendAsync(M.TableLeave);
    }

    private static async Task<(List<V4Client> Players, string MatchId)> ReadyThreeAsync(V4Env env)
    {
        var players = new List<V4Client>();
        for (var i = 0; i < 4; i++)
        {
            var c = await env.GuestAsync();
            Assert.True((await c.SendAsync(M.Hello)).Ok);
            players.Add(c);
        }
        (await players[0].Http.PostAsync($"/v1/players/{players[1].Account}/block", null)).EnsureSuccessStatusCode();
        foreach (var player in players)
            Assert.True((await player.SendAsync(M.RankedQueue, new { mode = "solo", party = Array.Empty<string>() })).Ok);
        await env.SettleAsync();
        var matchId = players[0].Last(M.RankedFound)!["found"].GetProperty("matchId").GetString()!;
        foreach (var player in players.Take(3)) Assert.True((await player.SendAsync(M.RankedAccept, new { matchId, accept = true })).Ok);
        return (players, matchId);
    }
}
