using System.Net;
using System.Net.Http.Json;
using Galaxy101.Contracts;

namespace Galaxy101.Server.Tests;

[Collection(ServerCollection.Name)]
public sealed class AuthTests(ServerFixture server)
{
    [Fact]
    public async Task Health_reports_database()
    {
        var response = await server.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Guest_gets_tokens_and_protected_calls_need_them()
    {
        var (client, tokens) = await server.GuestAsync();
        Assert.True(tokens.IsNewAccount);
        Assert.NotEmpty(tokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/v1/realtime/ticket", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await server.CreateClient().PostAsync("/v1/realtime/ticket", null)).StatusCode);
    }

    [Fact]
    public async Task Refresh_rotates_and_reuse_revokes_the_family()
    {
        var (_, tokens) = await server.GuestAsync();
        var anonymous = server.CreateClient();
        var first = await anonymous.PostAsJsonAsync("/v1/auth/refresh", new RefreshRequest { RefreshToken = tokens.RefreshToken });
        var rotated = (await first.Content.ReadFromJsonAsync<TokenResponse>())!;
        Assert.NotEqual(tokens.RefreshToken, rotated.RefreshToken);

        // Replaying the old token (stolen copy) fails and kills the newer one too.
        var replay = await anonymous.PostAsJsonAsync("/v1/auth/refresh", new RefreshRequest { RefreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        var after = await anonymous.PostAsJsonAsync("/v1/auth/refresh", new RefreshRequest { RefreshToken = rotated.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task Linked_provider_signs_back_into_the_same_account()
    {
        var (client, tokens) = await server.GuestAsync();
        var subject = "google:" + Guid.NewGuid();
        var link = await client.PostAsJsonAsync("/v1/auth/link", new ExternalLoginRequest { Provider = "google", IdToken = subject });
        Assert.Equal(HttpStatusCode.NoContent, link.StatusCode);

        var signIn = await server.CreateClient().PostAsJsonAsync("/v1/auth/external", new ExternalLoginRequest { Provider = "google", IdToken = subject });
        var again = (await signIn.Content.ReadFromJsonAsync<TokenResponse>())!;
        Assert.Equal(tokens.AccountId, again.AccountId);
        Assert.False(again.IsNewAccount);
        Assert.Contains("google", again.LinkedProviders);

        // The same Google identity cannot be attached to a second account.
        var (other, _) = await server.GuestAsync();
        var conflict = await other.PostAsJsonAsync("/v1/auth/link", new ExternalLoginRequest { Provider = "google", IdToken = subject });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task Logout_revokes_refresh()
    {
        var (client, tokens) = await server.GuestAsync();
        await client.PostAsJsonAsync("/v1/auth/logout", new RefreshRequest { RefreshToken = tokens.RefreshToken });
        var refresh = await server.CreateClient().PostAsJsonAsync("/v1/auth/refresh", new RefreshRequest { RefreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task Invalid_external_token_is_rejected()
    {
        var response = await server.CreateClient().PostAsJsonAsync("/v1/auth/external", new ExternalLoginRequest { Provider = "google", IdToken = "apple:x" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(ErrorCodes.InvalidToken, (await response.Content.ReadFromJsonAsync<ErrorResponse>())!.Code);
    }

    [Fact]
    public async Task Guest_gets_10000_chips_and_its_nickname_and_colour()
    {
        var client = server.CreateClient();
        var response = await client.PostAsJsonAsync("/v1/auth/guest", new GuestLoginRequest { Name = "Misafir4821", Color = "#3D7BFF" });
        var tokens = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        var wallet = (await client.GetFromJsonAsync<WalletResponse>("/v1/wallet"))!;
        Assert.Equal(10_000, wallet.Chips);
        var profile = (await client.GetFromJsonAsync<ProfileResponse>("/v1/profile"))!;
        Assert.Equal("Misafir4821", profile.Name); Assert.Equal("#3D7BFF", profile.Color);
        Assert.Equal(HttpStatusCode.BadRequest, (await server.CreateClient().PostAsJsonAsync("/v1/auth/guest", new GuestLoginRequest { Name = new string('a', 17) })).StatusCode);
    }

    [Fact]
    public async Task Register_then_login_with_password_and_three_wrong_tries_lock()
    {
        var email = $"oyuncu{Guid.NewGuid():N}".Substring(0, 20) + "@example.com";
        var anonymous = server.CreateClient();
        var register = await anonymous.PostAsJsonAsync("/v1/auth/register", new RegisterRequest { Name = "Deniz", Email = " " + email.ToUpperInvariant() + " ", Password = "gizli123", AcceptTerms = true });
        Assert.Equal(HttpStatusCode.OK, register.StatusCode);
        var tokens = (await register.Content.ReadFromJsonAsync<TokenResponse>())!;
        var client = server.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        Assert.Equal(25_000, (await client.GetFromJsonAsync<WalletResponse>("/v1/wallet"))!.Chips);

        Assert.Equal(HttpStatusCode.Conflict, (await anonymous.PostAsJsonAsync("/v1/auth/register", new RegisterRequest { Name = "Deniz", Email = email, Password = "gizli123", AcceptTerms = true })).StatusCode);
        var terms = await anonymous.PostAsJsonAsync("/v1/auth/register", new RegisterRequest { Name = "Deniz", Email = "x" + email, Password = "gizli123", AcceptTerms = false });
        Assert.Equal(ErrorCodes.TermsRequired, (await terms.Content.ReadFromJsonAsync<ErrorResponse>())!.Code);

        var ok = await anonymous.PostAsJsonAsync("/v1/auth/login", new PasswordLoginRequest { Email = email, Password = "gizli123" });
        Assert.Equal(tokens.AccountId, (await ok.Content.ReadFromJsonAsync<TokenResponse>())!.AccountId);

        var missing = await anonymous.PostAsJsonAsync("/v1/auth/login", new PasswordLoginRequest { Email = "yok" + email, Password = "gizli123" });
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(ErrorCodes.AccountNotFound, (await missing.Content.ReadFromJsonAsync<LoginErrorResponse>())!.Code);

        for (var left = 2; left >= 1; left--)
        {
            var wrong = await anonymous.PostAsJsonAsync("/v1/auth/login", new PasswordLoginRequest { Email = email, Password = "yanlis" });
            var body = (await wrong.Content.ReadFromJsonAsync<LoginErrorResponse>())!;
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode); Assert.Equal(ErrorCodes.WrongPassword, body.Code); Assert.Equal(left, body.RemainingAttempts);
        }
        var third = await anonymous.PostAsJsonAsync("/v1/auth/login", new PasswordLoginRequest { Email = email, Password = "yanlis" });
        Assert.Equal((HttpStatusCode)423, third.StatusCode);
        var locked = await anonymous.PostAsJsonAsync("/v1/auth/login", new PasswordLoginRequest { Email = email, Password = "gizli123" });
        var lockBody = (await locked.Content.ReadFromJsonAsync<LoginErrorResponse>())!;
        Assert.Equal(ErrorCodes.AccountLocked, lockBody.Code); Assert.True(lockBody.RetryAfter > 0, "even the right password waits");
    }
}
