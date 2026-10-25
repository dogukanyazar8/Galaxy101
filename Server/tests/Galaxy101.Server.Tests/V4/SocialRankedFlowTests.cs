using System.Net.Http.Json;
using Galaxy101.Contracts;
using M = Galaxy101.Contracts.V4Messages;

namespace Galaxy101.Server.Tests.V4;

/// <summary>Package flows with other people: invites (SRV-08) and the ranked queue (SRV-09, SRV-10).</summary>
[Collection(ServerCollection.Name)]
public sealed class SocialRankedFlowTests(ServerFixture server)
{
    private static async Task BefriendAsync(V4Client a, V4Client b)
    {
        var code = b.Last(M.Session)!["me"].GetProperty("code").GetString();
        (await a.Http.PostAsJsonAsync("/v1/friends/requests", new FriendCodeRequest { Code = code })).EnsureSuccessStatusCode();
        (await b.Http.PostAsync($"/v1/friends/requests/{a.Account}/accept", null)).EnsureSuccessStatusCode();
    }

    /// <summary>
    /// SRV-08 with real clients: the package's demo friend who accepts after 1.2–2 s (DEMO-07, DEMO-08) is a second real
    /// account here, which accepts the friendship over HTTP and the invite over the socket.
    /// </summary>
    [Fact]
    public async Task SRV_08_Invite_a_friend_valid_30_s_accepted_a_stranger_cannot_be_invited()
    {
        await using var env = new V4Env(server);
        var c = await env.RegisterAsync();
        var d = await env.RegisterAsync();
        var e = await env.RegisterAsync();
        foreach (var x in new[] { c, d, e }) await x.SendAsync(M.Hello);
        await BefriendAsync(c, d);
        await c.SendAsync(M.TableCreate, new { name = TableFlowTests.Unique("Davet"), mode = "solo", bet = 1000, hands = 3, turnSec = 30 });
        // Unanswered: after inviteTtl both sides hear it expired.
        var first = await c.SendAsync(M.InviteSend, new { userId = d.Account.ToString(), seat = 2 });
        Assert.True(first.Ok, first.Code);
        Assert.Equal(30000, first["expiresAt"].GetInt64() - env.Now);
        await env.SettleAsync();
        Assert.Equal(first.Str("inviteId"), d.Last(M.InviteIn)!.Str("inviteId"));
        await env.AdvanceAsync(30000);
        Assert.Equal("expired", c.Last(M.InviteResult)!.Str("result"));
        Assert.Equal(first.Str("inviteId"), d.Last(M.InviteExpired)!.Str("inviteId"));
        Assert.Equal(V4Errors.E_INVITE_EXPIRED, (await d.SendAsync(M.InviteRespond, new { inviteId = first.Str("inviteId"), accept = true })).Code);
        // Answered within the time: the friend sits on the invited seat.
        var a = await c.SendAsync(M.InviteSend, new { userId = d.Account.ToString(), seat = 2 });
        Assert.True(a.Ok);
        await env.AdvanceAsync(2500);
        Assert.True((await d.SendAsync(M.InviteRespond, new { inviteId = a.Str("inviteId"), accept = true })).Ok);
        await env.SettleAsync();
        var result = c.Last(M.InviteResult)!;
        Assert.Equal("accepted", result.Str("result"));
        Assert.Equal(2, result.Int("seat"));
        Assert.Equal(d.Account.ToString(), c.Last(M.TableState)!["table"].GetProperty("seats")[2].GetProperty("user").GetProperty("id").GetString());
        Assert.Equal(V4Errors.E_NOT_FRIENDS, (await c.SendAsync(M.InviteSend, new { userId = e.Account.ToString() })).Code);
        Assert.Equal(V4Errors.E_USER_UNKNOWN, (await c.SendAsync(M.InviteSend, new { userId = Guid.NewGuid().ToString() })).Code);
        Assert.Equal(V4Errors.E_USER_BUSY, (await c.SendAsync(M.InviteSend, new { userId = d.Account.ToString() })).Code);
        await d.SendAsync(M.TableLeave);
        await c.SendAsync(M.TableLeave);
    }

    private static async Task<List<V4Client>> QueueFourAsync(V4Env env)
    {
        var players = new List<V4Client>();
        for (var i = 0; i < 4; i++)
        {
            var p = await env.RegisterAsync();
            await p.SendAsync(M.Hello);
            Assert.True((await p.SendAsync(M.RankedQueue, new { mode = "solo", party = Array.Empty<string>() })).Ok);
            players.Add(p);
        }
        await env.SettleAsync();
        return players;
    }

    /// <summary>
    /// SRV-09 with a real queue: the package fills the table with bots that accept on a timer (DEMO-09); here four real
    /// accounts queue, are found together and accept. Three of them then drop and the server plays their seats (R-66).
    /// </summary>
    [Fact]
    public async Task SRV_09_Ranked_solo_search_found_accept_match_LP()
    {
        await using var env = new V4Env(server);
        var players = await QueueFourAsync(env);
        var c = players[0];
        Assert.Equal("solo", c.Of(M.RankedSearch)[0].Str("mode"));
        Assert.All(players, p => Assert.Single(p.Of(M.RankedFound)));
        var found = c.Last(M.RankedFound)!["found"];
        Assert.Equal(10000, found.GetProperty("expiresAt").GetInt64() - env.Now);
        await c.SendAsync(M.Resync);
        Assert.Equal("ranked.found", c.Last(M.Snapshot)!.Str("screen"));
        foreach (var p in players) Assert.True((await p.SendAsync(M.RankedAccept, new { matchId = found.GetProperty("matchId").GetString(), accept = true })).Ok);
        await env.SettleAsync();
        Assert.Equal(1800, c.Last(M.RankedReady)!["loadingMs"].GetInt64());
        Assert.True(c.Last(M.RankedReady)!["table"].GetProperty("ranked").GetBoolean());
        await c.SendAsync(M.Resync);
        Assert.Equal("ranked.loading", c.Last(M.Snapshot)!.Str("screen"));
        await env.AdvanceAsync(1800);
        Assert.Single(c.Of(M.MatchStart));
        foreach (var p in players.Skip(1)) await p.CloseAsync();
        Assert.True(await env.UntilAsync(() => c.Of(M.RankChange).Count > 0, 20000, 1000), "rankChange gelmedi");
        var rc = c.Last(M.RankChange)!;
        var mr = c.Last(M.MatchResult)!;
        var seat = c.Of(M.RankedReady)[0]["table"].GetProperty("seats").EnumerateArray().Select((s, i) => (s, i))
            .First(x => x.s.GetProperty("user").GetProperty("id").GetString() == c.Account.ToString()).i;
        Assert.Equal(mr["lp"][seat].GetInt32(), rc.Int("delta"));
        Assert.Contains(rc.Int("delta"), new[] { 30, 10, -10, -20, 20, 0, -15, 10, -7, 3 });
        Assert.Equal(0, mr["pool"].GetInt64());
        Assert.Equal(5, mr["history"].GetArrayLength());
    }

    [Fact]
    public async Task SRV_10_Ranked_accept_time_runs_out_back_out_of_the_found_match()
    {
        await using var env = new V4Env(server);
        var players = await QueueFourAsync(env);
        Assert.All(players, p => Assert.Single(p.Of(M.RankedFound)));
        await env.AdvanceAsync(10000);
        Assert.All(players, p => Assert.Equal("timeout", p.Last(M.RankedCancelled)?.Str("reason")));
        await players[0].SendAsync(M.Resync);
        Assert.Equal("menu", players[0].Last(M.Snapshot)!.Str("screen"));
        Assert.Equal(V4Errors.E_NOT_QUEUED, (await players[0].SendAsync(M.RankedCancel)).Code);
        // A decline sends the others back to searching.
        var again = await QueueFourAsync(env);
        var match = again[0].Last(M.RankedFound)!["found"].GetProperty("matchId").GetString();
        Assert.True((await again[0].SendAsync(M.RankedAccept, new { matchId = match, accept = false })).Ok);
        await env.SettleAsync();
        Assert.Equal("declined", again[0].Last(M.RankedCancelled)!.Str("reason"));
        Assert.Equal(2, again[1].Of(M.RankedSearch).Count);
        foreach (var p in again.Skip(1)) Assert.True((await p.SendAsync(M.RankedCancel)).Ok);
    }
}
