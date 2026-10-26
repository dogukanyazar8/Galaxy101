using System.Net.Http.Json;
using Galaxy101.Contracts;
using M = Galaxy101.Contracts.V4Messages;

namespace Galaxy101.Server.Tests.V4;

[Collection(ServerCollection.Name)]
public sealed class OwnerTransferTests(ServerFixture server)
{
    private static object Table(string name) => new { name, mode = "solo", bet = 1000, hands = 3, turnSec = 15 };
    private static async Task<long> ChipsAsync(V4Client client) => (await client.Http.GetFromJsonAsync<WalletResponse>("/v1/wallet"))!.Chips;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Waiting_owner_departure_transfers_to_first_human_and_preserves_other_stakes(bool disconnect)
    {
        await using var env = new V4Env(server);
        var owner = await env.GuestAsync();
        var successor = await env.GuestAsync();
        var other = await env.GuestAsync();
        var watcher = await env.GuestAsync();
        foreach (var client in new[] { owner, successor, other, watcher }) Assert.True((await client.SendAsync(M.Hello)).Ok);
        Assert.True((await watcher.SendAsync(M.LobbySubscribe)).Ok);
        var name = TableFlowTests.Unique("Owner transfer");
        var created = await owner.SendAsync(M.TableCreate, Table(name));
        Assert.True(created.Ok);
        var tableId = created["table"].GetProperty("id").GetString();
        Assert.True((await owner.SendAsync(M.SeatAddBot, new { seat = 1 })).Ok);
        // The first human in seat order wins, even when a higher seat joined earlier; bots are skipped.
        Assert.True((await other.SendAsync(M.TableJoin, new { tableId, seat = 3 })).Ok);
        Assert.True((await successor.SendAsync(M.TableJoin, new { tableId, seat = 2 })).Ok);

        if (disconnect) await env.DropAsync(owner);
        else Assert.True((await owner.SendAsync(M.TableLeave)).Ok);
        await env.SettleAsync();

        var probe = await env.Hub.ProbeAsync(name);
        Assert.NotNull(probe);
        Assert.Null(probe.SeatAccount[0]);
        Assert.True(probe.SeatBot[1]);
        Assert.Equal(2000, probe.BetsPaid);
        Assert.Equal(successor.Account.ToString(), successor.Last(M.TableState)!["table"].GetProperty("owner").GetString());
        Assert.Equal(successor.Account.ToString(), other.Last(M.TableState)!["table"].GetProperty("owner").GetString());
        Assert.Equal(successor.Account.ToString(), watcher.Last(M.LobbyTable)!["table"].GetProperty("owner").GetString());
        Assert.Empty(successor.Of(M.TableClosed));
        Assert.Empty(watcher.Of(M.LobbyTableRemoved));
        Assert.Equal(10000, await ChipsAsync(owner));
        Assert.Equal(9000, await ChipsAsync(successor));
        Assert.Equal(9000, await ChipsAsync(other));

        var formerOwner = owner;
        if (disconnect)
        {
            formerOwner = await env.ReconnectAsync(owner);
            Assert.True((await formerOwner.SendAsync(M.Hello)).Ok);
        }
        else Assert.Single(owner.Of(M.Balance), f => f.Str("reason") == "betRefund");
        Assert.Equal(V4Errors.E_NOT_SEATED, (await formerOwner.SendAsync(M.TableLeave)).Code);
        Assert.Equal(10000, await ChipsAsync(owner));
        Assert.True((await formerOwner.SendAsync(M.Resync)).Ok);
        Assert.Equal("menu", formerOwner.Last(M.Snapshot)!.Str("screen"));
        Assert.Equal(V4Errors.E_NOT_OWNER, (await other.SendAsync(M.SeatAddBot, new { seat = 0 })).Code);
        Assert.True((await successor.SendAsync(M.SeatKick, new { seat = 1 })).Ok);
        foreach (var seat in new[] { 0, 1 }) Assert.True((await successor.SendAsync(M.SeatAddBot, new { seat })).Ok);
        Assert.True((await successor.SendAsync(M.TableStart)).Ok);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Last_human_departure_closes_the_table_even_with_bots_and_spectators(bool disconnect)
    {
        await using var env = new V4Env(server);
        var owner = await env.GuestAsync();
        var spectator = await env.GuestAsync();
        Assert.True((await owner.SendAsync(M.Hello)).Ok);
        Assert.True((await spectator.SendAsync(M.Hello)).Ok);
        Assert.True((await spectator.SendAsync(M.LobbySubscribe)).Ok);
        var name = TableFlowTests.Unique("Last owner");
        var created = await owner.SendAsync(M.TableCreate, Table(name));
        Assert.True(created.Ok);
        var tableId = created["table"].GetProperty("id").GetString();
        foreach (var seat in new[] { 1, 2, 3 }) Assert.True((await owner.SendAsync(M.SeatAddBot, new { seat })).Ok);
        Assert.True((await spectator.SendAsync(M.TableSpectate, new { tableId })).Ok);

        if (disconnect) await env.DropAsync(owner);
        else Assert.True((await owner.SendAsync(M.TableLeave)).Ok);
        await env.SettleAsync();

        Assert.Null(await env.Hub.ProbeAsync(name));
        Assert.Equal(10000, await ChipsAsync(owner));
        Assert.Equal("ownerLeft", Assert.Single(spectator.Of(M.TableClosed)).Str("reason"));
        Assert.Equal(tableId, Assert.Single(spectator.Of(M.LobbyTableRemoved)).Str("id"));
        Assert.True((await spectator.SendAsync(M.Resync)).Ok);
        Assert.Equal("menu", spectator.Last(M.Snapshot)!.Str("screen"));
        Assert.Equal(V4Errors.E_TABLE_CLOSED, (await spectator.SendAsync(M.TableJoin, new { tableId })).Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Countdown_owner_departure_cancels_old_timer_before_the_new_owner_restarts(bool disconnect)
    {
        await using var env = new V4Env(server);
        var owner = await env.GuestAsync();
        var successor = await env.GuestAsync();
        Assert.True((await owner.SendAsync(M.Hello)).Ok);
        Assert.True((await successor.SendAsync(M.Hello)).Ok);
        var name = TableFlowTests.Unique("Countdown owner");
        var created = await owner.SendAsync(M.TableCreate, Table(name));
        Assert.True(created.Ok);
        var tableId = created["table"].GetProperty("id").GetString();
        Assert.True((await successor.SendAsync(M.TableJoin, new { tableId, seat = 2 })).Ok);
        foreach (var seat in new[] { 1, 3 }) Assert.True((await owner.SendAsync(M.SeatAddBot, new { seat })).Ok);
        Assert.True((await owner.SendAsync(M.TableStart)).Ok);
        await env.AdvanceAsync(1000);

        if (disconnect) await env.DropAsync(owner);
        else Assert.True((await owner.SendAsync(M.TableLeave)).Ok);
        await env.SettleAsync();

        Assert.NotNull(await env.Hub.ProbeAsync(name));
        var state = successor.Last(M.TableState)!["table"];
        Assert.Equal("waiting", state.GetProperty("status").GetString());
        Assert.Equal(0, state.GetProperty("countdown").GetInt32());
        Assert.Equal(successor.Account.ToString(), state.GetProperty("owner").GetString());
        Assert.Contains(successor.Of(M.Countdown), f => f.Has("cancelled") && f["cancelled"].GetBoolean());
        Assert.Equal(10000, await ChipsAsync(owner));
        Assert.Equal(9000, await ChipsAsync(successor));
        Assert.Equal(1000, (await env.Hub.ProbeAsync(name))!.BetsPaid);
        await env.AdvanceAsync(4000);
        Assert.Empty(successor.Of(M.MatchStart));
        Assert.Equal("waiting", successor.Last(M.TableState)!["table"].GetProperty("status").GetString());
        Assert.NotNull(await env.Hub.ProbeAsync(name));

        Assert.True((await successor.SendAsync(M.SeatAddBot, new { seat = 0 })).Ok);
        Assert.True((await successor.SendAsync(M.TableStart)).Ok);
        await env.AdvanceAsync(3000);
        Assert.Equal(successor.Account.ToString(), Assert.Single(successor.Of(M.MatchStart))["table"].GetProperty("owner").GetString());
    }
}
