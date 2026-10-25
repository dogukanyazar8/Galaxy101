using System.Net;
using System.Net.Http.Json;
using Galaxy101.Contracts;

namespace Galaxy101.Server.Tests;

[Collection(ServerCollection.Name)]
public sealed class ProfileTests(ServerFixture server)
{
    [Fact]
    public async Task New_guest_has_a_valid_default_profile()
    {
        var (client, tokens) = await server.GuestAsync();
        var profile = (await client.GetFromJsonAsync<ProfileResponse>("/v1/profile"))!;
        Assert.Equal(tokens.AccountId, profile.Id);
        Assert.Matches(@"^[A-Z]{2}-\d{4}$", profile.FriendCode);
        Assert.Equal(1, profile.Progress.Level);
        Assert.Equal("Satürn", profile.Planet);
        var wallet = (await client.GetFromJsonAsync<WalletResponse>("/v1/wallet"))!;
        Assert.Equal(10_000, wallet.Chips);
    }

    [Theory]
    [InlineData("ab", "#FF8A5B", 0)]          // name too short
    [InlineData("Valid Name", "#000000", 0)]  // colour not in palette
    [InlineData("Valid Name", "#FF8A5B", 2)]  // unknown frame
    [InlineData("Bad<script>", "#FF8A5B", 0)] // characters outside letters/digits/space/._
    public async Task Profile_update_rejects_invalid_values(string name, string color, int frame)
    {
        var (client, _) = await server.GuestAsync();
        var response = await client.PutAsJsonAsync("/v1/profile", new ProfileUpdateRequest { Name = name, Color = color, Frame = frame, Status = "" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Profile_update_trims_and_persists_turkish_names()
    {
        var (client, _) = await server.GuestAsync();
        var response = await client.PutAsJsonAsync("/v1/profile", new ProfileUpdateRequest { Name = "  Doğukan Ş. ", Color = "#16B6A8", Frame = 1, Status = "Okey'i bekliyorum" });
        response.EnsureSuccessStatusCode();
        var profile = (await client.GetFromJsonAsync<ProfileResponse>("/v1/profile"))!;
        Assert.Equal("Doğukan Ş.", profile.Name);
        Assert.Equal(1, profile.Frame);
    }

    [Fact]
    public async Task Settings_use_revisions_so_stale_devices_cannot_overwrite()
    {
        var (client, _) = await server.GuestAsync();
        var current = (await client.GetFromJsonAsync<SettingsResponse>("/v1/settings"))!;
        current.Settings.Music = 10;
        var saved = await client.PutAsJsonAsync("/v1/settings", new SettingsUpdateRequest { Settings = current.Settings, Revision = current.Revision });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        var stale = await client.PutAsJsonAsync("/v1/settings", new SettingsUpdateRequest { Settings = current.Settings, Revision = current.Revision });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        current.Settings.Warn = 99;
        var invalid = await client.PutAsJsonAsync("/v1/settings", new SettingsUpdateRequest { Settings = current.Settings, Revision = current.Revision + 1 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(10, (await client.GetFromJsonAsync<SettingsResponse>("/v1/settings"))!.Settings.Music);
    }

    [Fact]
    public async Task Trying_planets_progresses_the_traveller_badge()
    {
        var (client, _) = await server.GuestAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PutAsJsonAsync("/v1/settings/planet", new PlanetUpdateRequest { Planet = "Mars" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/v1/settings/planet", new PlanetUpdateRequest { Planet = "Pluto" })).StatusCode);
        var badges = (await client.GetFromJsonAsync<List<BadgeResponse>>("/v1/profile/badges"))!;
        Assert.Equal(2, badges.Single(x => x.Id == "galaxy_traveller").Current);
    }

    [Fact]
    public async Task Privacy_consent_must_match_the_current_notice()
    {
        var (client, _) = await server.GuestAsync();
        var info = (await client.GetFromJsonAsync<ServerInfoResponse>("/v1/info"))!;
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/v1/profile/privacy", new PrivacyConsentRequest { Version = "old" })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync("/v1/profile/privacy", new PrivacyConsentRequest { Version = info.PrivacyPolicyVersion })).StatusCode);
        Assert.Equal(info.PrivacyPolicyVersion, (await client.GetFromJsonAsync<ProfileResponse>("/v1/profile"))!.AcceptedPrivacyVersion);
    }

    [Fact]
    public async Task Deleting_the_account_removes_it_and_its_sessions()
    {
        var (client, tokens) = await server.GuestAsync();
        var ticket = (await (await client.PostAsync("/v1/realtime/ticket", null)).Content.ReadFromJsonAsync<RealtimeTicketResponse>())!;
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/v1/account")).StatusCode);
        // A ticket taken before the deletion opens no socket after it.
        await Assert.ThrowsAnyAsync<Exception>(() => server.Server.CreateWebSocketClient()
            .ConnectAsync(new Uri(server.Server.BaseAddress, "/v1/ws?ticket=" + ticket.Ticket), CancellationToken.None));
        // The account's still-unexpired access token is refused at once: no API, no socket ticket.
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/v1/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/v1/realtime/ticket", null)).StatusCode);
        var refresh = await server.CreateClient().PostAsJsonAsync("/v1/auth/refresh", new RefreshRequest { RefreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }
}
