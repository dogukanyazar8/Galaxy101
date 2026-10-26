using System.Net.Http.Json;
using System.Text.Json;
using Galaxy101.Contracts;
using M = Galaxy101.Contracts.V4Messages;

namespace Galaxy101.Server.Tests.V4;

[Collection(ServerCollection.Name)]
public sealed class ChatGiftFlowTests(ServerFixture server)
{
    private static object Table(string name, object? rules = null) => new { name, mode = "solo", bet = 1000, hands = 3, turnSec = 15, rules };
    private static async Task<long> ChipsAsync(V4Client client) => (await client.Http.GetFromJsonAsync<WalletResponse>("/v1/wallet"))!.Chips;

    // strings.json table.STR.quick.0..5 are the six HUD phrases; 6..8 remain accepted for existing protocol clients.
    [Theory]
    [InlineData("quick.0")]
    [InlineData("quick.1")]
    [InlineData("quick.2")]
    [InlineData("quick.3")]
    [InlineData("quick.4")]
    [InlineData("quick.5")]
    [InlineData("quick.6")]
    [InlineData("quick.7")]
    [InlineData("quick.8")]
    public async Task Quick_messages_keep_their_key_for_players_and_spectators(string quick)
    {
        await using var env = new V4Env(server);
        var sender = await env.GuestAsync();
        var recipient = await env.GuestAsync();
        var spectator = await env.GuestAsync();
        foreach (var client in new[] { sender, recipient, spectator }) Assert.True((await client.SendAsync(M.Hello)).Ok);
        var created = await sender.SendAsync(M.TableCreate, Table(TableFlowTests.Unique("Quick chat"), new { chat = "quick" }));
        Assert.True(created.Ok);
        var tableId = created["table"].GetProperty("id").GetString();
        Assert.True((await recipient.SendAsync(M.TableJoin, new { tableId })).Ok);
        Assert.True((await spectator.SendAsync(M.TableSpectate, new { tableId })).Ok);

        Assert.True((await sender.SendAsync(M.ChatSend, new { quick })).Ok);
        await env.SettleAsync();

        foreach (var client in new[] { sender, recipient, spectator })
        {
            var chat = Assert.Single(client.Of(M.Chat));
            Assert.Equal(quick, chat.Str("quick"));
            Assert.Equal(0, chat.Int("seat"));
            Assert.False(chat.Has("text"));
        }
    }

    [Theory]
    [InlineData("quick.9")]
    [InlineData("quick.00")]
    [InlineData("emoji.10")]
    public async Task Unknown_quick_keys_do_not_create_chat_lines(string quick)
    {
        await using var env = new V4Env(server);
        var sender = await env.GuestAsync();
        Assert.True((await sender.SendAsync(M.Hello)).Ok);
        Assert.True((await sender.SendAsync(M.TableCreate, Table(TableFlowTests.Unique("Bad quick")))).Ok);

        Assert.Equal(V4Errors.E_CHAT_QUICK_UNKNOWN, (await sender.SendAsync(M.ChatSend, new { quick })).Code);
        Assert.Empty(sender.Of(M.Chat));
    }

    [Fact]
    public async Task Targeted_and_everyone_gifts_charge_only_the_sender_once_per_recipient()
    {
        await using var env = new V4Env(server);
        var sender = await env.GuestAsync();
        var recipient = await env.GuestAsync();
        var spectator = await env.GuestAsync();
        var outsider = await env.GuestAsync();
        foreach (var client in new[] { sender, recipient, spectator, outsider }) Assert.True((await client.SendAsync(M.Hello)).Ok);
        var created = await sender.SendAsync(M.TableCreate, Table(TableFlowTests.Unique("Gift table")));
        Assert.True(created.Ok);
        var tableId = created["table"].GetProperty("id").GetString();
        Assert.True((await recipient.SendAsync(M.TableJoin, new { tableId, seat = 1 })).Ok);
        foreach (var seat in new[] { 2, 3 }) Assert.True((await sender.SendAsync(M.SeatAddBot, new { seat })).Ok);
        Assert.True((await spectator.SendAsync(M.TableSpectate, new { tableId })).Ok);
        Assert.True((await sender.SendAsync(M.TableStart)).Ok);
        Assert.True(await env.UntilAsync(() => sender.Of(M.Turn).Count > 0));

        Assert.True((await sender.SendAsync(M.GiftSend, new { gift = "tea", to = 1 })).Ok);
        await env.SettleAsync();

        foreach (var client in new[] { sender, recipient, spectator }) AssertGift(Assert.Single(client.Of(M.Gift)), "tea", [1], 100);
        Assert.Equal(8900, await ChipsAsync(sender));
        Assert.Single(sender.Of(M.Balance), f => f.Str("reason") == "gift");
        Assert.Equal(-100, sender.Last(M.Balance)!["dChips"].GetInt64());

        Assert.True((await sender.SendAsync(M.GiftSend, new { gift = "coffee", to = "all" })).Ok);
        await env.SettleAsync();

        foreach (var client in new[] { sender, recipient, spectator })
        {
            Assert.Equal(2, client.Of(M.Gift).Count);
            AssertGift(client.Last(M.Gift)!, "coffee", [1, 2, 3], 450);
        }
        Assert.Equal(8450, await ChipsAsync(sender));
        Assert.Equal(2, sender.Of(M.Balance).Count(f => f.Str("reason") == "gift"));
        Assert.Equal(-450, sender.Last(M.Balance)!["dChips"].GetInt64());
        Assert.Equal(9000, await ChipsAsync(recipient));
        Assert.Equal(10000, await ChipsAsync(spectator));
        Assert.DoesNotContain(recipient.Of(M.Balance), f => f.Str("reason") == "gift");
        Assert.Empty(spectator.Of(M.Balance));
        Assert.Empty(outsider.Of(M.Gift));
        Assert.True((await spectator.SendAsync(M.Resync)).Ok);
        var view = spectator.Last(M.Snapshot)!["game"];
        Assert.Equal(JsonValueKind.Null, view.GetProperty("rack").ValueKind);
        Assert.Equal(JsonValueKind.Null, view.GetProperty("legal").ValueKind);
        Assert.False(view.TryGetProperty("racks", out _));
        Assert.Equal(JsonValueKind.Number, view.GetProperty("deck").ValueKind);
    }

    [Fact]
    public async Task Disabled_gifts_do_not_charge_or_emit_a_gift()
    {
        await using var env = new V4Env(server);
        var sender = await env.GuestAsync();
        Assert.True((await sender.SendAsync(M.Hello)).Ok);
        Assert.True((await sender.SendAsync(M.TableCreate, Table(TableFlowTests.Unique("Gifts off"), new { gifts = false }))).Ok);
        Assert.True((await sender.SendAsync(M.SeatAddBot, new { seat = 1 })).Ok);

        Assert.Equal(V4Errors.E_GIFTS_OFF, (await sender.SendAsync(M.GiftSend, new { gift = "tea", to = 1 })).Code);

        Assert.Equal(9000, await ChipsAsync(sender));
        Assert.Empty(sender.Of(M.Gift));
        Assert.DoesNotContain(sender.Of(M.Balance), f => f.Str("reason") == "gift");
    }

    [Theory]
    [InlineData(false, 1000L)]
    [InlineData(true, 21000L)]
    public async Task Unaffordable_gifts_report_the_shortfall_without_charging(bool everyone, long missing)
    {
        await using var env = new V4Env(server);
        var sender = await env.GuestAsync();
        Assert.True((await sender.SendAsync(M.Hello)).Ok);
        Assert.True((await sender.SendAsync(M.TableCreate, Table(TableFlowTests.Unique("Gift funds")))).Ok);
        foreach (var seat in new[] { 1, 2, 3 }) Assert.True((await sender.SendAsync(M.SeatAddBot, new { seat })).Ok);

        var ack = await sender.SendAsync(M.GiftSend, new { gift = "rocket", to = everyone ? (object)"all" : 1 });

        Assert.Equal(V4Errors.E_NO_CHIPS, ack.Code);
        Assert.Equal(missing, ack["detail"].GetProperty("missing").GetInt64());
        Assert.Equal(9000, await ChipsAsync(sender));
        Assert.Empty(sender.Of(M.Gift));
        Assert.DoesNotContain(sender.Of(M.Balance), f => f.Str("reason") == "gift");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task Invalid_gift_targets_do_not_charge_or_broadcast(int to)
    {
        await using var env = new V4Env(server);
        var sender = await env.GuestAsync();
        Assert.True((await sender.SendAsync(M.Hello)).Ok);
        Assert.True((await sender.SendAsync(M.TableCreate, Table(TableFlowTests.Unique("Gift target")))).Ok);
        Assert.True((await sender.SendAsync(M.SeatAddBot, new { seat = 1 })).Ok);

        Assert.Equal(V4Errors.E_GIFT_TARGET, (await sender.SendAsync(M.GiftSend, new { gift = "tea", to })).Code);

        Assert.Equal(9000, await ChipsAsync(sender));
        Assert.Empty(sender.Of(M.Gift));
        Assert.DoesNotContain(sender.Of(M.Balance), f => f.Str("reason") == "gift");
    }

    private static void AssertGift(Frame frame, string gift, int[] to, long cost)
    {
        Assert.Equal(0, frame.Int("from"));
        Assert.Equal(gift, frame.Str("gift"));
        Assert.Equal(to, frame["to"].EnumerateArray().Select(s => s.GetInt32()));
        Assert.Equal(cost, frame["cost"].GetInt64());
        // A public flight carries no wallet or private hand data.
        Assert.Equal(new[] { "at", "cost", "from", "gift", "to" }, frame.Data.EnumerateObject().Select(p => p.Name).Order());
    }
}
