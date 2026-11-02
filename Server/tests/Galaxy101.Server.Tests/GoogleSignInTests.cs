using System.Net;
using System.Net.Http.Json;
using Galaxy101.Contracts;

namespace Galaxy101.Server.Tests;

/// <summary>Desktop "Google ile devam et": loopback authorization code + PKCE, exchanged by the server.</summary>
[Collection(ServerCollection.Name)]
public sealed class GoogleSignInTests(ServerFixture server)
{
    private static readonly string Verifier = new('v', 64);

    private Task<HttpResponseMessage> Post(string code, string redirect = "http://127.0.0.1:52817/", string? verifier = null) =>
        server.CreateClient().PostAsJsonAsync("/v1/auth/google/code", new GoogleCodeRequest { Code = code, CodeVerifier = verifier ?? Verifier, RedirectUri = redirect });

    private HttpClient Bearer(TokenResponse tokens)
    {
        var client = server.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", tokens.AccessToken);
        return client;
    }

    [Fact]
    public async Task Providers_name_the_desktop_client_id()
    {
        var providers = await server.CreateClient().GetFromJsonAsync<AuthProvidersResponse>("/v1/auth/providers");
        Assert.Equal("desktop-client.apps.googleusercontent.com", providers!.GoogleDesktopClientId);
    }

    [Fact]
    public async Task A_new_google_identity_gets_a_registered_account_named_after_its_first_name()
    {
        var subject = Guid.NewGuid().ToString("N");
        var response = await Post("ok|google:" + subject + ":Doğukan");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var tokens = (await response.Content.ReadFromJsonAsync<TokenResponse>())!;
        Assert.True(tokens.IsNewAccount);
        Assert.Contains("google", tokens.LinkedProviders);
        Assert.Equal(("ok|google:" + subject + ":Doğukan", Verifier, "http://127.0.0.1:52817/"), server.GoogleCodes.Last);
        Assert.Equal("Doğukan", (await Bearer(tokens).GetFromJsonAsync<ProfileResponse>("/v1/profile"))!.Name);
        Assert.Equal(25_000, (await Bearer(tokens).GetFromJsonAsync<WalletResponse>("/v1/wallet"))!.Chips);

        // The same Google account signs back into it.
        var again = (await (await Post("ok|google:" + subject)).Content.ReadFromJsonAsync<TokenResponse>())!;
        Assert.Equal(tokens.AccountId, again.AccountId);
        Assert.False(again.IsNewAccount);
    }

    [Fact]
    public async Task An_invalid_google_name_falls_back_to_a_player_name()
    {
        var tokens = (await (await Post("ok|google:" + Guid.NewGuid().ToString("N") + ":<script>")).Content.ReadFromJsonAsync<TokenResponse>())!;
        Assert.StartsWith("Oyuncu ", (await Bearer(tokens).GetFromJsonAsync<ProfileResponse>("/v1/profile"))!.Name);
    }

    [Fact]
    public async Task A_guest_that_linked_google_signs_back_in_through_the_desktop_flow()
    {
        var (client, guest) = await server.GuestAsync();
        var subject = Guid.NewGuid().ToString("N");
        var link = await client.PostAsJsonAsync("/v1/auth/link", new ExternalLoginRequest { Provider = "google", IdToken = "google:" + subject });
        Assert.Equal(HttpStatusCode.NoContent, link.StatusCode);
        var tokens = (await (await Post("ok|google:" + subject)).Content.ReadFromJsonAsync<TokenResponse>())!;
        Assert.Equal(guest.AccountId, tokens.AccountId);
    }

    [Theory]
    [InlineData("http://localhost:52817/")]       // a host name another program could answer for
    [InlineData("https://evil.example/cb")]
    [InlineData("http://127.0.0.1/")]             // no port: the default port is not the game's listener
    [InlineData("http://127.0.0.1:52817/?x=1")]
    public async Task Only_loopback_redirects_are_exchanged(string redirect)
    {
        var response = await Post("ok|google:x", redirect);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(V4Errors.E_OAUTH_FAILED, (await response.Content.ReadFromJsonAsync<ErrorResponse>())!.Error);
    }

    [Fact]
    public async Task A_short_pkce_verifier_is_refused() =>
        Assert.Equal(HttpStatusCode.BadRequest, (await Post("ok|google:x", verifier: "short")).StatusCode);

    [Fact]
    public async Task A_refused_code_or_a_token_for_another_provider_is_unauthorized()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Post("expired")).StatusCode);
        var wrong = await Post("ok|apple:" + Guid.NewGuid());
        Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        Assert.Equal(ErrorCodes.InvalidToken, (await wrong.Content.ReadFromJsonAsync<ErrorResponse>())!.Code);
    }

    [Fact]
    public async Task Google_being_unreachable_is_a_retryable_503()
    {
        var response = await Post("down");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(ErrorCodes.ProviderUnavailable, (await response.Content.ReadFromJsonAsync<ErrorResponse>())!.Code);
    }
}
