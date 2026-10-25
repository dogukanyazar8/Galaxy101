using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Galaxy101.Contracts;
using M = Galaxy101.Contracts.V4Messages;

namespace Galaxy101.Server.Tests.V4;

/// <summary>
/// Package flows of accounts and economy. The package's register / login / guest / oauth / passwordReset / dailyClaim /
/// questClaim / leaderboardGet messages map to the HTTP endpoints (docs/v4/PROTOKOL_ESLEME.md); the protocol's error code
/// comes in the error body's <c>error</c>.
/// </summary>
[Collection(ServerCollection.Name)]
public sealed class AccountEconomyFlowTests(ServerFixture server)
{
    private static async Task<string?> ErrorOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).TryGetProperty("error", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : null;

    private async Task<(HttpClient Client, TokenResponse Tokens, string Email)> RegisterAsync(string password = "123456")
    {
        var email = $"{Guid.NewGuid():N}@g.co";
        var client = server.CreateClient();
        var response = await client.PostAsJsonAsync("/v1/auth/register", new RegisterRequest { Name = "L" + Guid.NewGuid().ToString("N")[..8], Email = email, Password = password, AcceptTerms = true });
        response.EnsureSuccessStatusCode();
        var tokens = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        return (client, tokens, email);
    }

    private Task<HttpResponseMessage> Login(string email, string password) =>
        server.CreateClient().PostAsJsonAsync("/v1/auth/login", new PasswordLoginRequest { Email = email, Password = password });

    private static async Task<long> Chips(HttpClient c) => (await c.GetFromJsonAsync<WalletResponse>("/v1/wallet"))!.Chips;

    /// <summary>dailyClaim's amount: what the claim added to the wallet.</summary>
    private static async Task<(HttpStatusCode Status, long Amount, string? Error)> Claim(HttpClient c)
    {
        var before = await Chips(c);
        var response = await c.PostAsync("/v1/daily/claim", null);
        return (response.StatusCode, await Chips(c) - before, response.IsSuccessStatusCode ? null : await ErrorOf(response));
    }

    [Fact]
    public async Task SRV_12_Daily_reward_once_a_day_quest_not_claimable_before_done()
    {
        var (client, tokens) = await server.GuestAsync();
        var first = await Claim(client);
        Assert.Equal((HttpStatusCode.OK, 1000L), (first.Status, first.Amount));
        var second = await Claim(client);
        Assert.Equal((HttpStatusCode.Conflict, V4Errors.E_DAILY_CLAIMED), (second.Status, second.Error));
        await server.AdvanceAsync(TimeSpan.FromDays(1), client, tokens);
        Assert.Equal(2000, (await Claim(client)).Amount);
        // Package quest q.win3 = server quest "winHands" (3 hands, 2.000 chips).
        var quest = await client.PostAsync("/v1/daily/quests/winHands/claim", null);
        Assert.Equal(V4Errors.E_QUEST_INCOMPLETE, await ErrorOf(quest));
        Assert.Equal(V4Errors.E_QUEST_UNKNOWN, await ErrorOf(await client.PostAsync("/v1/daily/quests/q.nope/claim", null)));
    }

    [Fact]
    public async Task SRV_14_Login_unknown_account_wrong_password_with_tries_left_locked_at_the_third()
    {
        var (_, _, email) = await RegisterAsync();
        var unknown = await Login("x" + email, "1");
        Assert.Equal((HttpStatusCode.NotFound, V4Errors.E_ACCOUNT_UNKNOWN), (unknown.StatusCode, await ErrorOf(unknown)));
        var wrong = await (await Login(email, "x")).Content.ReadFromJsonAsync<LoginErrorResponse>();
        Assert.Equal(V4Errors.E_PASS_WRONG, wrong!.Error);
        Assert.Equal(2, ((JsonElement)wrong.Detail).GetProperty("left").GetInt32());
        await Login(email, "x");
        var locked = await Login(email, "x");
        Assert.Equal((HttpStatusCode.Locked, V4Errors.E_ACCOUNT_LOCKED), (locked.StatusCode, await ErrorOf(locked)));
    }

    [Fact]
    public async Task SRV_19_Lock_lasts_5_minutes_and_a_password_reset_lifts_it()
    {
        var (_, _, email) = await RegisterAsync();
        for (var i = 0; i < 3; i++) await Login(email, "x");
        var locked = await (await Login(email, "123456")).Content.ReadFromJsonAsync<LoginErrorResponse>();
        Assert.Equal(V4Errors.E_ACCOUNT_LOCKED, locked!.Error);
        Assert.Equal(server.Clock.GetUtcNow().AddMilliseconds(300000).ToUnixTimeMilliseconds(), ((JsonElement)locked.Detail).GetProperty("until").GetInt64());
        server.Clock.Advance(TimeSpan.FromMilliseconds(300000));
        Assert.Equal(HttpStatusCode.OK, (await Login(email, "123456")).StatusCode);
        for (var i = 0; i < 3; i++) await Login(email, "x");
        Assert.Equal(HttpStatusCode.Locked, (await Login(email, "123456")).StatusCode);
        var anonymous = server.CreateClient();
        Assert.Equal(HttpStatusCode.NoContent, (await anonymous.PostAsJsonAsync("/v1/auth/password/forgot", new PasswordForgotRequest { Email = email })).StatusCode);
        var badCode = await anonymous.PostAsJsonAsync("/v1/auth/password/reset", new PasswordResetRequest { Email = email, Code = "000000", Password = "yeni123" });
        Assert.Equal(V4Errors.E_RESET_CODE, await ErrorOf(badCode));
        var reset = await anonymous.PostAsJsonAsync("/v1/auth/password/reset", new PasswordResetRequest { Email = email, Code = server.Mail.CodeFor(email), Password = "yeni123" });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Login(email, "yeni123")).StatusCode);
    }

    [Fact]
    public async Task SRV_20_Starting_balance_guest_10000_account_25000_everyone_20_gems()
    {
        await using var env = new V4Env(server);
        var guest = await env.GuestAsync("Konuk");
        var account = await env.RegisterAsync();
        foreach (var c in new[] { guest, account }) await c.SendAsync(M.Hello);
        Assert.Equal(10000, guest.Last(M.Session)!["me"].GetProperty("chips").GetInt64());
        Assert.Equal(20, guest.Last(M.Session)!["me"].GetProperty("gems").GetInt64());
        Assert.Equal(25000, account.Last(M.Session)!["me"].GetProperty("chips").GetInt64());
        Assert.Equal(20, account.Last(M.Session)!["me"].GetProperty("gems").GetInt64());
        var google = server.CreateClient();
        var tokens = (await (await google.PostAsJsonAsync("/v1/auth/external", new ExternalLoginRequest { Provider = "google", IdToken = "google:" + Guid.NewGuid() }))
            .Content.ReadFromJsonAsync<TokenResponse>())!;
        google.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        var wallet = (await google.GetFromJsonAsync<WalletResponse>("/v1/wallet"))!;
        Assert.Equal((25000L, 20L), (wallet.Chips, wallet.Gems));
        var failed = await server.CreateClient().PostAsJsonAsync("/v1/auth/external", new ExternalLoginRequest { Provider = "google", IdToken = "nope" });
        Assert.Equal(V4Errors.E_OAUTH_FAILED, await ErrorOf(failed));
    }

    [Fact]
    public async Task SRV_21_Weekly_points_30_15_5_1_shared_on_ties_not_against_bots_season_is_LP()
    {
        await using var env = new V4Env(server);
        env.Hub.SeedSource = () => 42;
        var c = await env.RegisterAsync();
        await c.SendAsync(M.Hello);
        await c.SendAsync(M.VsBotsStart, new { mode = "solo", hands = 1 });
        Assert.True(await env.UntilAsync(() => c.Of(M.MatchResult).Count > 0, 4000));
        Assert.Equal(0, c.Last(M.MatchResult)!["weekly"][0].GetInt32());
        Assert.Equal(0, (await c.Http.GetFromJsonAsync<LeaderboardResponse>("/v1/leaderboard/weekly"))!.Me.Points);
        await c.SendAsync(M.TableCreate, new { name = TableFlowTests.Unique("H"), mode = "solo", bet = 1000, hands = 3, turnSec = 15 });
        foreach (var s in new[] { 1, 2, 3 }) await c.SendAsync(M.SeatAddBot, new { seat = s });
        await c.SendAsync(M.TableStart);
        Assert.True(await env.UntilAsync(() => c.Of(M.MatchResult).Count == 2, 12000));
        var mr = c.Last(M.MatchResult)!;
        var units = mr["ranking"].EnumerateArray().ToList();
        var place = units.First(u => u.GetProperty("seats").EnumerateArray().Any(x => x.GetInt32() == 0)).GetProperty("rank").GetInt32();
        var tied = units.Count(u => u.GetProperty("rank").GetInt32() == place);
        int[] w = [30, 15, 5, 1];
        var want = (int)Math.Floor(w.Skip(place - 1).Take(tied).Average() + 0.5);
        Assert.Equal(want, mr["weekly"][0].GetInt32());
        var board = (await c.Http.GetFromJsonAsync<LeaderboardResponse>("/v1/leaderboard/weekly"))!;
        Assert.Equal(want, board.Me.Points);
        var season = (await c.Http.GetFromJsonAsync<LeaderboardResponse>("/v1/leaderboard/season"))!;
        Assert.True(season.Rows.Zip(season.Rows.Skip(1)).All(p => p.First.Points >= p.Second.Points));
    }

    [Fact]
    public async Task SRV_22_Day_turns_over_at_Istanbul_midnight_a_missed_day_restarts_the_streak_day_8_is_day_1()
    {
        var (client, tokens) = await server.GuestAsync();
        // 23:59:59.999 in Istanbul (UTC+3) of the current server day.
        var now = server.Clock.GetUtcNow();
        var local = now.ToOffset(TimeSpan.FromHours(3));
        var midnight = new DateTimeOffset(local.Date.AddDays(1), TimeSpan.FromHours(3));
        tokens = await server.AdvanceAsync(midnight - now - TimeSpan.FromMilliseconds(1), client, tokens);
        Assert.Equal(1000, (await Claim(client)).Amount);
        server.Clock.Advance(TimeSpan.FromMilliseconds(2));
        Assert.Equal(2000, (await Claim(client)).Amount);
        tokens = await server.AdvanceAsync(TimeSpan.FromDays(2), client, tokens);
        Assert.Equal(1000, (await Claim(client)).Amount);
        for (var i = 0; i < 6; i++) { tokens = await server.AdvanceAsync(TimeSpan.FromDays(1), client, tokens); await Claim(client); }
        tokens = await server.AdvanceAsync(TimeSpan.FromDays(1), client, tokens);
        Assert.Equal(1000, (await Claim(client)).Amount);
    }

    [Fact]
    public async Task SRV_24_XP_finisher_340_others_60_place_bonus_500_200_80_0_level_curve_375n()
    {
        await using var env = new V4Env(server);
        env.Hub.SeedSource = () => 42;
        var c = await env.RegisterAsync();
        await c.SendAsync(M.Hello);
        await c.SendAsync(M.VsBotsStart, new { mode = "solo", hands = 2 });
        Assert.True(await env.UntilAsync(() => c.Of(M.MatchResult).Count > 0, 4000));
        await env.SettleAsync();
        var hands = c.Game("handEnd").Select(f => f.Ev.GetProperty("result").GetProperty("finisher").GetInt32() == 0 ? 340L : 60L).ToList();
        Assert.Equal(2, hands.Count);
        var xs = c.Of(M.Xp);
        Assert.Equal(hands, xs.Where(x => x.Str("reason") == "hand").Select(x => x["gained"].GetInt64()));
        var mr = c.Last(M.MatchResult)!;
        var bonus = xs.Where(x => x.Str("reason") == "match").Select(x => x["gained"].GetInt64()).FirstOrDefault();
        Assert.Equal(mr["xpBonus"][0].GetInt64(), bonus);
        Assert.Contains(bonus, new long[] { 500, 200, 80, 0, 350, 140, 40, 260, 93, 195 });
        var last = xs[^1];
        Assert.Equal(375 * last.Int("level"), last["xpNext"].GetInt64());
        var profile = await c.Http.GetFromJsonAsync<JsonElement>("/v1/profile");
        Assert.Equal(hands.Sum() + bonus, profile.GetProperty("progress").GetProperty("xp").GetInt64());
    }
}
