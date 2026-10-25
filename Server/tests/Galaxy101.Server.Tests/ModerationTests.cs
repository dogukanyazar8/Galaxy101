using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Galaxy101.Contracts;
using Galaxy101.Server.Social;

namespace Galaxy101.Server.Tests;

[Collection(ServerCollection.Name)]
public sealed class ModerationTests(ServerFixture server)
{
    // ---- Şifremi unuttum ----------------------------------------------------------------------------------------

    [Fact]
    public async Task A_forgotten_password_is_reset_with_the_mailed_code_and_every_session_ends()
    {
        var email = $"unutkan{Guid.NewGuid():N}"[..20] + "@example.com";
        var anonymous = server.CreateClient();
        var register = await anonymous.PostAsJsonAsync("/v1/auth/register", new RegisterRequest { Name = "Unutkan", Email = email, Password = "eskisifre", AcceptTerms = true });
        var tokens = (await register.Content.ReadFromJsonAsync<TokenResponse>())!;

        Assert.Equal(HttpStatusCode.NoContent, (await anonymous.PostAsJsonAsync("/v1/auth/password/forgot", new PasswordForgotRequest { Email = email.ToUpperInvariant() })).StatusCode);
        var code = server.Mail.CodeFor(email);
        Assert.Matches("^[0-9]{6}$", code);
        // A second request within the minute sends nothing new; the first code still works.
        Assert.Equal(HttpStatusCode.NoContent, (await anonymous.PostAsJsonAsync("/v1/auth/password/forgot", new PasswordForgotRequest { Email = email })).StatusCode);
        Assert.Equal(1, server.Mail.CountFor(email));

        var wrong = await anonymous.PostAsJsonAsync("/v1/auth/password/reset", new PasswordResetRequest { Email = email, Code = code == "000000" ? "111111" : "000000", Password = "yenisifre" });
        Assert.Equal(ErrorCodes.CodeInvalid, (await wrong.Content.ReadFromJsonAsync<ErrorResponse>())!.Code);
        Assert.Equal(HttpStatusCode.NoContent, (await anonymous.PostAsJsonAsync("/v1/auth/password/reset", new PasswordResetRequest { Email = email, Code = code, Password = "yenisifre" })).StatusCode);
        // One use only.
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync("/v1/auth/password/reset", new PasswordResetRequest { Email = email, Code = code, Password = "baskasifre" })).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/v1/auth/login", new PasswordLoginRequest { Email = email, Password = "eskisifre" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.PostAsJsonAsync("/v1/auth/login", new PasswordLoginRequest { Email = email, Password = "yenisifre" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/v1/auth/refresh", new RefreshRequest { RefreshToken = tokens.RefreshToken })).StatusCode);
    }

    [Fact]
    public async Task Reset_answers_alike_for_unknown_e_mails_and_codes_expire_and_burn_after_five_tries()
    {
        var anonymous = server.CreateClient();
        var unknown = $"yok{Guid.NewGuid():N}"[..20] + "@example.com";
        Assert.Equal(HttpStatusCode.NoContent, (await anonymous.PostAsJsonAsync("/v1/auth/password/forgot", new PasswordForgotRequest { Email = unknown })).StatusCode);
        Assert.Equal(0, server.Mail.CountFor(unknown));
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync("/v1/auth/password/forgot", new PasswordForgotRequest { Email = "not-an-email" })).StatusCode);

        var email = $"deneme{Guid.NewGuid():N}"[..20] + "@example.com";
        await anonymous.PostAsJsonAsync("/v1/auth/register", new RegisterRequest { Name = "Deneme", Email = email, Password = "sifre123", AcceptTerms = true });
        await anonymous.PostAsJsonAsync("/v1/auth/password/forgot", new PasswordForgotRequest { Email = email });
        var code = server.Mail.CodeFor(email)!;
        server.Clock.Advance(TimeSpan.FromMinutes(16));
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync("/v1/auth/password/reset", new PasswordResetRequest { Email = email, Code = code, Password = "yenisifre" })).StatusCode);

        await anonymous.PostAsJsonAsync("/v1/auth/password/forgot", new PasswordForgotRequest { Email = email });
        var fresh = server.Mail.CodeFor(email)!;
        var wrongCode = fresh == "123456" ? "654321" : "123456";
        for (var i = 0; i < 5; i++)
            await anonymous.PostAsJsonAsync("/v1/auth/password/reset", new PasswordResetRequest { Email = email, Code = wrongCode, Password = "yenisifre" });
        Assert.Equal(HttpStatusCode.BadRequest, (await anonymous.PostAsJsonAsync("/v1/auth/password/reset", new PasswordResetRequest { Email = email, Code = fresh, Password = "yenisifre" })).StatusCode);
    }

    // ---- Profanity ----------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("siktir git", "****** git")]
    [InlineData("S1KT1R lan", "****** lan")]
    [InlineData("o r o s p u", "* * * * * *")]
    [InlineData("siiiiktir", "*********")]
    [InlineData("what the fuck", "what the ****")]
    [InlineData("Eşyaları götürmek lazım", "Eşyaları götürmek lazım")]
    [InlineData("Sikke koleksiyonu", "Sikke koleksiyonu")]
    [InlineData("I got it, nice move!", "I got it, nice move!")]
    [InlineData("Güzel hamle!", "Güzel hamle!")]
    public void Chat_masks_profanity_but_not_ordinary_words(string text, string expected) => Assert.Equal(expected, TextFilter.Mask(text));

    [Fact]
    public async Task Names_and_status_lines_refuse_profanity()
    {
        var anonymous = server.CreateClient();
        var guest = await anonymous.PostAsJsonAsync("/v1/auth/guest", new GuestLoginRequest { Name = "Orospu", Platform = "test" });
        Assert.Equal(ErrorCodes.Inappropriate, (await guest.Content.ReadFromJsonAsync<ErrorResponse>())!.Code);
        var (client, _) = await server.GuestAsync();
        var profile = (await client.GetFromJsonAsync<ProfileResponse>("/v1/profile"))!;
        var update = new ProfileUpdateRequest { Name = profile.Name, Color = profile.Color, Status = "siktir", Frame = 0 };
        var refused = await client.PutAsJsonAsync("/v1/profile", update);
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Equal(ErrorCodes.Inappropriate, (await refused.Content.ReadFromJsonAsync<ErrorResponse>())!.Code);
        update.Status = "Hep hazırım";
        Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync("/v1/profile", update)).StatusCode);
    }

    // ---- Blocks, reports, bans ------------------------------------------------------------------------------------

    [Fact]
    public async Task A_block_ends_the_friendship_and_stops_requests_until_lifted()
    {
        var (a, aTokens) = await server.GuestAsync();
        var (b, bTokens) = await server.GuestAsync();
        await SocialTests.MakeFriends(a, b);
        var aCode = (await a.GetFromJsonAsync<ProfileResponse>("/v1/profile"))!.FriendCode;

        Assert.Equal(HttpStatusCode.NoContent, (await a.PostAsync($"/v1/players/{bTokens.AccountId}/block", null)).StatusCode);
        Assert.Empty((await a.GetFromJsonAsync<FriendsResponse>("/v1/friends"))!.Friends);
        Assert.Equal(bTokens.AccountId, Assert.Single((await a.GetFromJsonAsync<List<PlayerSummary>>("/v1/players/blocks"))!).Id);
        // The blocked player cannot ask again; the block reads as "no such player".
        Assert.Equal(HttpStatusCode.NotFound, (await b.PostAsJsonAsync("/v1/friends/requests", new FriendCodeRequest { Code = aCode })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await a.PostAsync($"/v1/players/{aTokens.AccountId}/block", null)).StatusCode); // not yourself

        Assert.Equal(HttpStatusCode.NoContent, (await a.DeleteAsync($"/v1/players/{bTokens.AccountId}/block")).StatusCode);
        Assert.Empty((await a.GetFromJsonAsync<List<PlayerSummary>>("/v1/players/blocks"))!);
        Assert.Equal(HttpStatusCode.NoContent, (await b.PostAsJsonAsync("/v1/friends/requests", new FriendCodeRequest { Code = aCode })).StatusCode);
    }

    [Fact]
    public async Task Reports_reach_moderators_once_a_day_per_pair_and_the_admin_side_needs_its_key()
    {
        var (reporter, _) = await server.GuestAsync();
        var (_, reportedTokens) = await server.GuestAsync();
        var report = new ReportRequest { Reason = "chat", Details = "Küfür etti" };
        Assert.Equal(HttpStatusCode.NoContent, (await reporter.PostAsJsonAsync($"/v1/players/{reportedTokens.AccountId}/report", report)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await reporter.PostAsJsonAsync($"/v1/players/{reportedTokens.AccountId}/report", report)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await reporter.PostAsJsonAsync($"/v1/players/{reportedTokens.AccountId}/report", new ReportRequest { Reason = "spam?" })).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await server.CreateClient().GetAsync("/v1/admin/reports")).StatusCode);
        var wrongKey = server.CreateClient(); wrongKey.DefaultRequestHeaders.Add("X-Admin-Key", "nope");
        Assert.Equal(HttpStatusCode.NotFound, (await wrongKey.GetAsync("/v1/admin/reports")).StatusCode);

        var admin = Admin();
        var mine = (await admin.GetFromJsonAsync<List<ReportResponse>>("/v1/admin/reports"))!.Where(r => r.Reported.Id == reportedTokens.AccountId).ToList();
        Assert.Equal("chat", Assert.Single(mine).Reason);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/v1/admin/reports/{mine[0].Id}/resolve", null)).StatusCode);
        Assert.DoesNotContain((await admin.GetFromJsonAsync<List<ReportResponse>>("/v1/admin/reports"))!, r => r.Id == mine[0].Id);
    }

    [Fact]
    public async Task A_ban_signs_the_player_out_refuses_renewal_and_lifting_it_restores_the_session()
    {
        var (player, tokens) = await server.GuestAsync();
        var admin = Admin();
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsJsonAsync($"/v1/admin/accounts/{tokens.AccountId}/ban", new BanRequest { Minutes = 60, Reason = "küfür" })).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await player.GetAsync("/v1/profile")).StatusCode);
        var renew = await server.CreateClient().PostAsJsonAsync("/v1/auth/refresh", new RefreshRequest { RefreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Forbidden, renew.StatusCode);
        var banned = (await renew.Content.ReadFromJsonAsync<LoginErrorResponse>())!;
        Assert.Equal(ErrorCodes.Banned, banned.Code);
        Assert.InRange(banned.RetryAfter, 3500, 3600);

        // The refresh token was not spent: it works again once the ban is lifted.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.PostAsync($"/v1/admin/accounts/{tokens.AccountId}/unban", null)).StatusCode);
        var back = await server.CreateClient().PostAsJsonAsync("/v1/auth/refresh", new RefreshRequest { RefreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, back.StatusCode);
        player.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", (await back.Content.ReadFromJsonAsync<TokenResponse>())!.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await player.GetAsync("/v1/profile")).StatusCode);
    }

    private HttpClient Admin()
    {
        var admin = server.CreateClient();
        admin.DefaultRequestHeaders.Add("X-Admin-Key", ServerFixture.AdminKey);
        return admin;
    }
}
