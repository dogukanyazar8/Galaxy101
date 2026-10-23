using System.Net;
using System.Net.Http.Json;
using Galaxy101.Contracts;

namespace Galaxy101.Server.Tests;

/// <summary>Klasik lobby (spec/20): open, list, PIN, code, seats, bots, kick, countdown, the match keeps the table's rules.</summary>
[Collection(ServerCollection.Name)]
public sealed class LobbyTests(ServerFixture server)
{
    private sealed record Player(HttpClient Http, TokenResponse Tokens, RealtimeClient Live);

    private async Task<Player> ConnectAsync()
    {
        var (http, tokens) = await server.GuestAsync();
        return new Player(http, tokens, await RealtimeClient.ConnectAsync(server, http));
    }

    private static TableOptions Options(string name, string visibility = "public", string? pin = null, long stake = 1000) => new()
    {
        Type = "pairs", Name = name, Visibility = visibility, Pin = pin, Hands = 3, Stake = stake, TurnSeconds = 15, Chat = "off", Gifts = false, OkeyPenalty = false,
    };

    [Fact]
    public async Task Open_list_pin_bots_kick_and_start_with_the_tables_rules()
    {
        var host = await ConnectAsync(); var guest = await ConnectAsync(); var watcher = await ConnectAsync();
        try
        {
            Assert.True((await watcher.Live.RequestAsync(LobbyMessageTypes.Subscribe, new { })).Ok);
            await watcher.Live.WaitAsync<LobbyTablesMessage>(LobbyMessageTypes.Tables);

            Assert.Equal(ErrorCodes.Validation, (await host.Live.RequestAsync(LobbyMessageTypes.Create, new TableCreateMessage { Options = Options("") })).Code);
            Assert.Equal(ErrorCodes.Validation, (await host.Live.RequestAsync(LobbyMessageTypes.Create, new TableCreateMessage { Options = Options("Masa", pin: "12a4") })).Code);
            Assert.True((await host.Live.RequestAsync(LobbyMessageTypes.Create, new TableCreateMessage { Options = Options("Doğukan'ın masası", pin: "1234") })).Ok);
            var room = await host.Live.WaitAsync<TableStateMessage>(LobbyMessageTypes.State);
            Assert.Matches("^GX-[A-Z0-9]{4}$", room.Code); Assert.Equal(0, room.YourSeat); Assert.True(room.Table.Locked);

            // Listed live for others.
            var listed = await watcher.Live.WaitAsync<LobbyTablesMessage>(LobbyMessageTypes.Tables, m => m.Tables.Any(t => t.Id == room.Table.Id));
            var card = listed.Tables.Single(t => t.Id == room.Table.Id);
            Assert.Equal("waiting", card.Status); Assert.Equal(1000, card.Stake); Assert.Equal(3, card.Hands); Assert.Equal(15, card.TurnSeconds); Assert.Equal("pairs", card.Type);
            Assert.False(card.OkeyPenalty); Assert.True(card.Penalties);

            Assert.Equal(TableErrors.WrongPin, (await guest.Live.RequestAsync(LobbyMessageTypes.Join, new TableJoinMessage { TableId = room.Table.Id, Pin = "0000" })).Code);
            Assert.True((await guest.Live.RequestAsync(LobbyMessageTypes.Join, new TableJoinMessage { TableId = room.Table.Id, Pin = "1234", Seat = 2 })).Ok);
            var seatedState = await guest.Live.WaitAsync<TableStateMessage>(LobbyMessageTypes.State);
            Assert.Equal(2, seatedState.YourSeat); Assert.Equal(room.Code, seatedState.Code);

            // Only the host adds bots or removes people.
            Assert.Equal(TableErrors.NotHost, (await guest.Live.RequestAsync(LobbyMessageTypes.AddBot, new TableSeatMessage { Seat = 1 })).Code);
            Assert.True((await host.Live.RequestAsync(LobbyMessageTypes.Kick, new TableSeatMessage { Seat = 2 })).Ok);
            Assert.Equal("kicked", (await guest.Live.WaitAsync<TableStateMessage>(LobbyMessageTypes.State, m => m.Closed)).Reason);
            guest.Live.Forget(LobbyMessageTypes.State);
            Assert.True((await guest.Live.RequestAsync(LobbyMessageTypes.Join, new TableJoinMessage { Code = room.Code.Substring(3).ToLowerInvariant(), Pin = "1234", Seat = 2 })).Ok); // by code, prefix optional

            Assert.Equal(TableErrors.NotReady, (await host.Live.RequestAsync(LobbyMessageTypes.Start, new { })).Code);
            Assert.True((await host.Live.RequestAsync(LobbyMessageTypes.AddBot, new TableSeatMessage { Seat = 1 })).Ok);
            var third = await host.Live.RequestAsync(LobbyMessageTypes.AddBot, new TableSeatMessage { Seat = 3 });
            Assert.True(third.Ok, third.Code);
            var full = await host.Live.WaitAsync<TableStateMessage>(LobbyMessageTypes.State, m => m.Countdown > 0);
            Assert.Equal("full", full.Table.Status); Assert.True(full.Table.Seats[1].Bot);
            Assert.True((await host.Live.RequestAsync(LobbyMessageTypes.Start, new { })).Ok);
            // The test server's clock is manual: let the 3 s countdown run out.
            for (var i = 0; i < 8; i++) { server.Clock.Advance(TimeSpan.FromSeconds(1)); await Task.Delay(50); }

            var starts = await Task.WhenAll(host.Live.WaitAsync<MatchStartMessage>(MessageTypes.MatchStart, null, 15), guest.Live.WaitAsync<MatchStartMessage>(MessageTypes.MatchStart, null, 15));
            Assert.Equal(starts[0].MatchId, starts[1].MatchId);
            Assert.Equal(GameModes.Pairs, starts[0].Mode); Assert.Equal(3, starts[0].Hands); Assert.Equal(15, starts[0].TurnSeconds);
            Assert.Equal(0, starts[0].YourSeat); Assert.Equal(2, starts[1].YourSeat);
            Assert.StartsWith("Bot · ", starts[0].Seats[1].Player.Name);
            // The room's penalty switches reach the engine and every seat's view.
            var view = await host.Live.WaitAsync<MatchStateMessage>(MessageTypes.MatchState, null, 15);
            Assert.NotNull(view.View.Rules); Assert.False(view.View.Rules!.OkeyPenalty); Assert.True(view.View.Rules.PlayablePenalty);
            // The table's rules: chat and gifts off.
            Assert.Equal(ErrorCodes.Validation, (await host.Live.RequestAsync(MessageTypes.MatchEmote, new EmoteMessage { MatchId = starts[0].MatchId, Emote = "😂" })).Code);
            var listedPlaying = await watcher.Live.WaitAsync<LobbyTablesMessage>(LobbyMessageTypes.Tables, m => m.Tables.Any(t => t.Id == room.Table.Id && t.Status == "playing"));
            Assert.Equal(1, listedPlaying.Tables.Single(t => t.Id == room.Table.Id).Hand);
            // The host paid the stake; bots did not.
            Assert.Equal(9_000, (await host.Http.GetFromJsonAsync<WalletResponse>("/v1/wallet"))!.Chips);
        }
        finally
        {
            foreach (var p in new[] { host, guest })
                try { await p.Live.SendAsync(MessageTypes.MatchLeave, new MatchRefMessage { MatchId = (await p.Http.GetFromJsonAsync<MatchRefMessage>("/v1/matches/current"))?.MatchId ?? Guid.Empty }); } catch { }
            await host.Live.DisposeAsync(); await guest.Live.DisposeAsync(); await watcher.Live.DisposeAsync();
        }
    }

    [Fact]
    public async Task Private_tables_hide_from_the_list_and_stakes_need_the_chips()
    {
        var host = await ConnectAsync(); var other = await ConnectAsync();
        try
        {
            Assert.Equal(ErrorCodes.InsufficientFunds, (await host.Live.RequestAsync(LobbyMessageTypes.Create, new TableCreateMessage { Options = Options("Büyük", stake: 100_000) })).Code);
            Assert.True((await host.Live.RequestAsync(LobbyMessageTypes.Create, new TableCreateMessage { Options = Options("Gizli", "private") })).Ok);
            var room = await host.Live.WaitAsync<TableStateMessage>(LobbyMessageTypes.State);
            Assert.True((await other.Live.RequestAsync(LobbyMessageTypes.Subscribe, new { })).Ok);
            var list = await other.Live.WaitAsync<LobbyTablesMessage>(LobbyMessageTypes.Tables);
            Assert.DoesNotContain(list.Tables, t => t.Id == room.Table.Id);
            Assert.Equal(TableErrors.Closed, (await other.Live.RequestAsync(LobbyMessageTypes.Join, new TableJoinMessage { TableId = room.Table.Id })).Code); // private: code only
            Assert.True((await other.Live.RequestAsync(LobbyMessageTypes.Join, new TableJoinMessage { Code = room.Code })).Ok);
            Assert.Equal(TableErrors.Closed, (await other.Live.RequestAsync(LobbyMessageTypes.Join, new TableJoinMessage { Code = "GX-ZZZZ" })).Code);

            // The host leaves: the other player becomes host; when everyone leaves the table closes.
            Assert.True((await host.Live.RequestAsync(LobbyMessageTypes.Leave, new { })).Ok);
            var handed = await other.Live.WaitAsync<TableStateMessage>(LobbyMessageTypes.State, m => m.Table.HostId == other.Tokens.AccountId);
            Assert.Equal(1, handed.YourSeat);
            Assert.True((await other.Live.RequestAsync(LobbyMessageTypes.Leave, new { })).Ok);
            Assert.Equal(TableErrors.Closed, (await host.Live.RequestAsync(LobbyMessageTypes.Join, new TableJoinMessage { Code = room.Code })).Code);
        }
        finally { await host.Live.DisposeAsync(); await other.Live.DisposeAsync(); }
    }

    [Fact]
    public async Task Recent_players_can_be_invited_and_listed()
    {
        var (http, _) = await server.GuestAsync();
        var recent = await http.GetFromJsonAsync<List<FriendResponse>>("/v1/players/recent");
        Assert.NotNull(recent);
        Assert.Equal(HttpStatusCode.NotFound, (await http.PostAsJsonAsync($"/v1/friends/{Guid.NewGuid()}/invite", new InviteRequest { RoomCode = "GX-AAAA" })).StatusCode);
    }
}
