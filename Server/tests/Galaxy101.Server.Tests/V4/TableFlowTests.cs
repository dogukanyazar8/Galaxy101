using System.Net.Http.Json;
using System.Text.Json;
using Galaxy101.Contracts;
using M = Galaxy101.Contracts.V4Messages;

namespace Galaxy101.Server.Tests.V4;

/// <summary>Package flows around tables: joining, the waiting room, leaving, disconnecting, taking a seat, chat and katlamalı.</summary>
[Collection(ServerCollection.Name)]
public sealed class TableFlowTests(ServerFixture server)
{
    private static object Table(string name, string mode = "solo", long bet = 1000, int hands = 3, int turnSec = 15, string? pin = null,
        bool @private = false, bool katlama = false, bool? katlamaPartner = null, object? rules = null) =>
        new { name, mode, bet, hands, turnSec, pin, @private, katlama, katlamaPartner, rules };

    internal static string Unique(string name) => name + "-" + Guid.NewGuid().ToString("N")[..6];

    private static async Task<long> ChipsAsync(V4Client c) => (await c.Http.GetFromJsonAsync<WalletResponse>("/v1/wallet"))!.Chips;

    [Fact]
    public async Task SRV_04_Pin_and_code_wrong_pin_refused_right_pin_sits_stake_taken_and_given_back()
    {
        await using var env = new V4Env(server);
        var owner = await env.RegisterAsync();
        var c = await env.GuestAsync();
        await owner.SendAsync(M.Hello);
        await c.SendAsync(M.Hello);
        var chips0 = c.Last(M.Session)!["me"].GetProperty("chips").GetInt64();
        Assert.Equal(10000, chips0);
        var name = Unique("Akşam keyfi");
        var created = await owner.SendAsync(M.TableCreate, Table(name, mode: "pairs", bet: 10000, hands: 7, turnSec: 30, pin: "1234"));
        Assert.True(created.Ok);
        var tableId = created["table"].GetProperty("id").GetString();
        Assert.True(created["table"].GetProperty("locked").GetBoolean());
        Assert.Equal(V4Errors.E_PIN_WRONG, (await c.SendAsync(M.TableJoin, new { tableId, pin = "0000" })).Code);
        Assert.True((await c.SendAsync(M.TableJoin, new { tableId, pin = "1234" })).Ok);
        Assert.Equal(chips0 - 10000, c.Last(M.Balance)!["chips"].GetInt64());
        Assert.Equal("bet", c.Last(M.Balance)!.Str("reason"));
        Assert.Equal(chips0 - 10000, await ChipsAsync(c));
        Assert.True((await c.SendAsync(M.TableLeave)).Ok);
        Assert.Equal(chips0, c.Last(M.Balance)!["chips"].GetInt64());
        Assert.Equal("betRefund", c.Last(M.Balance)!.Str("reason"));
        Assert.Equal(chips0, await ChipsAsync(c));
        Assert.Equal(V4Errors.E_CODE_FORMAT, (await c.SendAsync(M.TableJoinCode, new { code = "XX" })).Code);
        Assert.Equal(V4Errors.E_CODE_UNKNOWN, (await c.SendAsync(M.TableJoinCode, new { code = "GX-0000" })).Code);
        // A private table by its code (the package's GX-7Q2M is demo data: a real owner's table here).
        await owner.SendAsync(M.TableLeave);
        var room = await owner.SendAsync(M.TableCreate, Table(Unique("Özel oda"), @private: true, turnSec: 20));
        var code = room["table"].GetProperty("code").GetString()!;
        Assert.Matches("^GX-[A-Z0-9]{4}$", code);
        var joined = await c.SendAsync(M.TableJoinCode, new { code = code.ToLowerInvariant() });
        Assert.True(joined.Ok);
        Assert.Equal(1, joined.Int("seat"));
        await c.SendAsync(M.TableLeave);
        await owner.SendAsync(M.TableLeave);
    }

    [Fact]
    public async Task SRV_05_Table_form_validation_and_not_enough_chips()
    {
        await using var env = new V4Env(server);
        var c = await env.GuestAsync();
        await c.SendAsync(M.Hello);
        Assert.Equal(V4Errors.E_TABLE_NAME, (await c.SendAsync(M.TableCreate, Table(" "))).Code);
        Assert.Equal(V4Errors.E_PIN_FORMAT, (await c.SendAsync(M.TableCreate, Table("x", pin: "12"))).Code);
        Assert.Equal(V4Errors.E_TABLE_OPTIONS, (await c.SendAsync(M.TableCreate, Table("x", bet: 2000))).Code);
        Assert.Equal(V4Errors.E_TABLE_OPTIONS, (await c.SendAsync(M.TableCreate, Table("x", turnSec: 25))).Code);
        var poor = await c.SendAsync(M.TableCreate, Table("x", bet: 50000));
        Assert.Equal(V4Errors.E_NO_CHIPS, poor.Code);
        Assert.Equal(40000, poor["detail"].GetProperty("missing").GetInt64());
    }

    [Fact]
    public async Task SRV_06_Waiting_room_bots_start_countdown_3_2_1_then_the_match()
    {
        await using var env = new V4Env(server);
        var c = await env.GuestAsync();
        await c.SendAsync(M.Hello);
        await c.SendAsync(M.TableCreate, Table(Unique("Test"), mode: "pairs"));
        Assert.Equal(V4Errors.E_TABLE_NOT_FULL, (await c.SendAsync(M.TableStart)).Code);
        foreach (var s in new[] { 1, 2, 3 }) Assert.True((await c.SendAsync(M.SeatAddBot, new { seat = s })).Ok);
        Assert.Equal(V4Errors.E_SEAT_TAKEN, (await c.SendAsync(M.SeatAddBot, new { seat = 1 })).Code);
        Assert.True((await c.SendAsync(M.TableStart)).Ok);
        await env.AdvanceAsync(3000);
        Assert.Equal([3, 2, 1], c.Of(M.Countdown).Select(x => x.Int("n")));
        Assert.Equal([3000L, 2000L, 1000L], c.Of(M.Countdown).Select(x => x["startsIn"].GetInt64()));
        Assert.Single(c.Of(M.MatchStart));
        Assert.Equal(3000, c.Of(M.MatchStart)[0]["countdownMs"].GetInt64());
        await c.SendAsync(M.TableLeave);
    }

    [Fact]
    public async Task SRV_07_Leaving_the_match_keeps_the_stake_in_the_pot_and_a_bot_plays_on()
    {
        await using var env = new V4Env(server);
        var c = await env.GuestAsync();
        await c.SendAsync(M.Hello);
        var name = Unique("Test");
        await c.SendAsync(M.TableCreate, Table(name));
        foreach (var s in new[] { 1, 2, 3 }) await c.SendAsync(M.SeatAddBot, new { seat = s });
        await c.SendAsync(M.TableStart);
        Assert.True(await env.UntilAsync(() => c.Of(M.Turn).Count > 0));
        var chips = c.Last(M.Balance)!["chips"].GetInt64();
        Assert.True((await c.SendAsync(M.TableLeave)).Ok);
        Assert.Single(c.Of(M.LeftTable));
        Assert.Equal(1300, c.Of(M.LeftTable)[0]["toLobbyIn"].GetInt64());
        Assert.Equal(chips, c.Last(M.Balance)!["chips"].GetInt64());
        Assert.Equal(chips, await ChipsAsync(c));
        var probe = (await env.Hub.ProbeAsync(name))!;
        Assert.True(probe.SeatBot[0]);
        Assert.True(probe.Left[0]);
        Assert.True(await env.UntilAsync(() => env.Hub.ProbeAsync(name).Result is null, 12000), "maç bitmedi");
    }

    [Fact]
    public async Task SRV_13_Chat_rules_off_and_quick_only()
    {
        await using var env = new V4Env(server);
        var c = await env.GuestAsync();
        await c.SendAsync(M.Hello);
        await c.SendAsync(M.TableCreate, Table(Unique("S"), turnSec: 30, rules: new { chat = "quick" }));
        Assert.Equal(V4Errors.E_CHAT_QUICK_ONLY, (await c.SendAsync(M.ChatSend, new { text = "selam" })).Code);
        Assert.True((await c.SendAsync(M.ChatSend, new { quick = "quick.1" })).Ok);
        Assert.Equal(V4Errors.E_CHAT_QUICK_UNKNOWN, (await c.SendAsync(M.ChatSend, new { quick = "quick.99" })).Code);
        Assert.Equal("quick.1", c.Last(M.Chat)!.Str("quick"));
        await c.SendAsync(M.TableLeave);
        await c.SendAsync(M.TableCreate, Table(Unique("S2"), rules: new { chat = "off" }));
        Assert.Equal(V4Errors.E_CHAT_OFF, (await c.SendAsync(M.ChatSend, new { quick = "quick.1" })).Code);
        await c.SendAsync(M.TableLeave);
        await c.SendAsync(M.TableCreate, Table(Unique("S3")));
        Assert.Equal(V4Errors.E_CHAT_LENGTH, (await c.SendAsync(M.ChatSend, new { text = new string('a', 121) })).Code);
        Assert.True((await c.SendAsync(M.ChatSend, new { text = new string('a', 120) })).Ok);
        Assert.Equal(V4Errors.E_CHAT_LENGTH, (await c.SendAsync(M.ChatSend, new { text = "   " })).Code);
        await c.SendAsync(M.TableLeave);
        Assert.Equal(V4Errors.E_NOT_SEATED, (await c.SendAsync(M.ChatSend, new { quick = "quick.1" })).Code);
    }

    [Fact]
    public async Task SRV_16_A_disconnected_player_is_not_out_the_server_plays_after_2_timeouts_and_stops_on_return()
    {
        await using var env = new V4Env(server);
        var c = await env.GuestAsync();
        var o = await env.RegisterAsync();
        await c.SendAsync(M.Hello);
        await o.SendAsync(M.Hello);
        var name = Unique("K");
        var created = await c.SendAsync(M.TableCreate, Table(name));
        Assert.True((await o.SendAsync(M.TableJoin, new { tableId = created["table"].GetProperty("id").GetString() })).Ok);
        foreach (var s in new[] { 2, 3 }) await c.SendAsync(M.SeatAddBot, new { seat = s });
        await c.SendAsync(M.TableStart);
        Assert.True(await env.UntilAsync(() => c.Of(M.Turn).Count > 0));
        const int oSeat = 1;
        await o.CloseAsync();
        await env.WaitAsync(async () => !(await env.Hub.ProbeAsync(name))!.Connected[oSeat]);
        Assert.False((await env.Hub.ProbeAsync(name))!.Left[oSeat]);
        Assert.Contains(c.Of(M.SeatConnection), f => f.Int("seat") == oSeat && !f["connected"].GetBoolean());
        Assert.True(await env.UntilAsync(() => env.Hub.ProbeAsync(name).Result!.Autoplay[oSeat], 3000), "otomatik oyuna geçmedi");
        Assert.Equal(2, c.Game("auto").Count(f => f.Ev.GetProperty("seat").GetInt32() == oSeat));
        Assert.Contains(c.Of(M.SeatConnection), f => f.Int("seat") == oSeat && f.Has("autoplay") && f["autoplay"].GetBoolean());
        var d = await env.ReconnectAsync(o);
        Assert.True((await d.SendAsync(M.Hello, new { token = "x" })).Ok);
        Assert.False((await env.Hub.ProbeAsync(name))!.Autoplay[oSeat]);
        Assert.Equal("table", d.Last(M.Snapshot)!.Str("screen"));
        await env.SettleAsync();
        Assert.Contains(c.Of(M.SeatConnection), f => f.Int("seat") == oSeat && f["connected"].GetBoolean() && !f["autoplay"].GetBoolean());
        await d.SendAsync(M.TableLeave);
        await c.SendAsync(M.TableLeave);
    }

    [Fact]
    public async Task SRV_17_A_waiting_room_disconnect_removes_the_player_at_once_and_returns_the_stake()
    {
        await using var env = new V4Env(server);
        var c = await env.GuestAsync();
        var o = await env.RegisterAsync();
        await c.SendAsync(M.Hello);
        await o.SendAsync(M.Hello);
        var name = Unique("W");
        var created = await c.SendAsync(M.TableCreate, Table(name, turnSec: 30));
        await o.SendAsync(M.TableJoin, new { tableId = created["table"].GetProperty("id").GetString() });
        Assert.Equal(24000, await ChipsAsync(o));
        await o.CloseAsync();
        await env.WaitAsync(async () => (await env.Hub.ProbeAsync(name))!.SeatAccount[1] is null);
        var probe = (await env.Hub.ProbeAsync(name))!;
        Assert.Null(probe.SeatAccount[1]);
        Assert.Equal(25000, await ChipsAsync(o));
        Assert.Equal(1000, probe.BetsPaid);
        var back = await env.ReconnectAsync(o);
        await back.SendAsync(M.Hello);
        Assert.Equal("menu", back.Last(M.Snapshot)!.Str("screen"));
        await c.SendAsync(M.TableLeave);
    }

    [Fact]
    public async Task SRV_18_Taking_a_seat_of_a_running_match_pays_the_stake_now_and_keeps_the_seat_total()
    {
        await using var env = new V4Env(server);
        var c = await env.GuestAsync();
        var n = await env.RegisterAsync();
        await c.SendAsync(M.Hello);
        await n.SendAsync(M.Hello);
        var name = Unique("D");
        var created = await c.SendAsync(M.TableCreate, Table(name));
        var tableId = created["table"].GetProperty("id").GetString();
        foreach (var s in new[] { 1, 2, 3 }) await c.SendAsync(M.SeatAddBot, new { seat = s });
        await c.SendAsync(M.TableStart);
        Assert.True(await env.UntilAsync(() => env.Hub.ProbeAsync(name).Result?.MatchStatus == "handOver", 6000));
        var total = (await env.Hub.ProbeAsync(name))!.Totals[1];
        var a = await n.SendAsync(M.TableJoin, new { tableId });
        Assert.True(a.Ok, a.Code);
        Assert.Equal(1, a.Int("seat"));
        Assert.Equal(24000, await ChipsAsync(n));
        var probe = (await env.Hub.ProbeAsync(name))!;
        Assert.Equal(total, probe.Totals[1]);
        Assert.Equal(2000, probe.BetsPaid);
        Assert.False(probe.SeatBot[1]);
        Assert.Equal("table", n.Last(M.Snapshot)!.Str("screen"));
        Assert.Contains(n.Game("seatTaken"), f => f.Ev.GetProperty("seat").GetInt32() == 1);
        var vsOwner = await env.GuestAsync();
        await vsOwner.SendAsync(M.Hello);
        var vs = await vsOwner.SendAsync(M.VsBotsStart, new { mode = "solo", hands = 1 });
        Assert.Equal(V4Errors.E_TABLE_PLAYING, await TakeVsBotsSeat(env, vs));
        await vsOwner.SendAsync(M.TableLeave);
        Assert.True(await env.UntilAsync(() => env.Hub.ProbeAsync(name).Result is null, 12000), "maç bitmedi");
        var result = Assert.Single(n.Of(M.MatchResult));
        Assert.Equal(2000, result["pool"].GetInt64());
    }

    /// <summary>A newcomer may not take a seat at an against-bots table (E_TABLE_PLAYING).</summary>
    private static async Task<string?> TakeVsBotsSeat(V4Env env, Frame vs)
    {
        var newcomer = await env.GuestAsync();
        await newcomer.SendAsync(M.Hello);
        return (await newcomer.SendAsync(M.TableJoin, new { tableId = vs["table"].GetProperty("id").GetString() })).Code;
    }

    [Fact]
    public async Task SRV_23_Katlamali_option_on_the_card_and_in_the_engine_partner_option_defaults_on()
    {
        await using var env = new V4Env(server);
        var c = await env.GuestAsync();
        await c.SendAsync(M.Hello);
        var a = await c.SendAsync(M.TableCreate, Table(Unique("Kat"), mode: "pairs", turnSec: 30, katlama: true));
        Assert.True(a["table"].GetProperty("katlama").GetBoolean());
        Assert.True(a["table"].GetProperty("katlamaPartner").GetBoolean());
        await c.SendAsync(M.TableLeave);
        var solo = await c.SendAsync(M.TableCreate, Table(Unique("KatSolo"), turnSec: 30, katlama: true));
        Assert.Equal(JsonValueKind.Null, solo["table"].GetProperty("katlamaPartner").ValueKind);
        await c.SendAsync(M.TableLeave);
        var name = Unique("Kat2");
        var b = await c.SendAsync(M.TableCreate, Table(name, mode: "pairs", turnSec: 30, katlama: true, katlamaPartner: false));
        Assert.False(b["table"].GetProperty("katlamaPartner").GetBoolean());
        foreach (var s in new[] { 1, 2, 3 }) await c.SendAsync(M.SeatAddBot, new { seat = s });
        await c.SendAsync(M.TableStart);
        await env.AdvanceAsync(3500);
        var probe = (await env.Hub.ProbeAsync(name))!;
        Assert.True(probe.Katlama);
        Assert.False(probe.KatlamaPartner);
        Assert.Equal(V4Errors.E_ALREADY_SEATED, (await c.SendAsync(M.TableCreate, Table("x", turnSec: 30, katlama: true))).Code);
        await c.SendAsync(M.TableLeave);
    }

    [Fact]
    public async Task SRV_25_Pairs_leaver_team_not_last_leaver_gets_nothing_partner_takes_the_team_prize_no_rake()
    {
        await using var env = new V4Env(server);
        env.Hub.SeedSource = () => 13;
        var c = await env.GuestAsync();
        var p = await env.RegisterAsync();
        await c.SendAsync(M.Hello);
        await p.SendAsync(M.Hello);
        var created = await c.SendAsync(M.TableCreate, Table(Unique("E"), mode: "pairs"));
        Assert.True((await p.SendAsync(M.TableJoin, new { tableId = created["table"].GetProperty("id").GetString(), seat = 2 })).Ok);
        foreach (var s in new[] { 1, 3 }) await c.SendAsync(M.SeatAddBot, new { seat = s });
        await c.SendAsync(M.TableStart);
        Assert.True(await env.UntilAsync(() => p.Of(M.Turn).Count > 0));
        var pChips = await ChipsAsync(p);
        await p.SendAsync(M.TableLeave);
        var px = p.Of(M.Xp).Count;
        Assert.True(await env.UntilAsync(() => c.Of(M.MatchResult).Count > 0, 12000));
        var mr = c.Last(M.MatchResult)!;
        var ranking = mr["ranking"].EnumerateArray().ToList();
        Assert.Equal(2, ranking.Count);
        var team0 = ranking.First(u => u.GetProperty("seats").EnumerateArray().Any(x => x.GetInt32() == 0));
        // R-91 / D-34: the pool is the stakes paid (two people here); the team's whole share goes to the partner still seated.
        Assert.Equal(2000, mr["pool"].GetInt64());
        Assert.Equal(team0.GetProperty("rank").GetInt32() == 1 ? 2000 : 0, mr["payouts"][0].GetInt64());
        Assert.Equal(0, mr["payouts"][2].GetInt64());
        Assert.Equal(px, p.Of(M.Xp).Count);
        Assert.Equal(0, mr["rake"].GetInt32());
        Assert.Equal(pChips, await ChipsAsync(p));
    }
}
