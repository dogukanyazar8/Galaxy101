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
}
