using System.Net;
using System.Net.Http.Json;
using Galaxy101.Contracts;
using Galaxy101.Server.Data;
using Galaxy101.Server.Social;
using Microsoft.Extensions.DependencyInjection;

namespace Galaxy101.Server.Tests;

[Collection(ServerCollection.Name)]
public sealed class SocialTests(ServerFixture server)
{
    [Fact]
    public async Task Friend_request_by_code_reaches_the_target_live_and_accepting_links_both()
    {
        var (alice, _) = await server.GuestAsync();
        var (bob, bobTokens) = await server.GuestAsync();
        var bobProfile = (await bob.GetFromJsonAsync<ProfileResponse>("/v1/profile"))!;
        await using var bobLive = await RealtimeClient.ConnectAsync(server, bob);

        // Codes are accepted in the loose forms people type.
        var loose = "#" + bobProfile.FriendCode.Replace("-", " ").ToLowerInvariant();
        Assert.Equal(HttpStatusCode.NoContent, (await alice.PostAsJsonAsync("/v1/friends/requests", new FriendCodeRequest { Code = loose })).StatusCode);
        var pushed = await bobLive.WaitAsync<FriendRequestResponse>(MessageTypes.FriendRequest);
        var aliceId = pushed.Player.Id;

        var bobView = (await bob.GetFromJsonAsync<FriendsResponse>("/v1/friends"))!;
        Assert.Equal(aliceId, Assert.Single(bobView.Incoming).Player.Id);
        Assert.Equal(HttpStatusCode.NoContent, (await bob.PostAsync($"/v1/friends/requests/{aliceId}/accept", null)).StatusCode);

        var aliceView = (await alice.GetFromJsonAsync<FriendsResponse>("/v1/friends"))!;
        var friend = Assert.Single(aliceView.Friends);
        Assert.Equal(bobTokens.AccountId, friend.Player.Id);
        Assert.Equal("online", friend.Presence);
        Assert.Equal(HttpStatusCode.Conflict, (await alice.PostAsJsonAsync("/v1/friends/requests", new FriendCodeRequest { Code = bobProfile.FriendCode })).StatusCode);
    }

    [Fact]
    public async Task Presence_changes_are_pushed_to_online_friends()
    {
        var (alice, aliceTokens) = await server.GuestAsync();
        var (bob, _) = await server.GuestAsync();
        await MakeFriends(alice, bob);
        await using var aliceLive = await RealtimeClient.ConnectAsync(server, alice);

        var bobLive = await RealtimeClient.ConnectAsync(server, bob);
        var online = await aliceLive.WaitAsync<PresenceMessage>(MessageTypes.Presence, p => p.Presence == "online");
        await bobLive.DisposeAsync();
        var offline = await aliceLive.WaitAsync<PresenceMessage>(MessageTypes.Presence, p => p.Presence == "offline");
        Assert.Equal(online.AccountId, offline.AccountId);
        Assert.NotEqual(aliceTokens.AccountId, online.AccountId);
    }

    [Fact]
    public async Task Asking_back_accepts_and_declining_removes()
    {
        var (alice, aliceTokens) = await server.GuestAsync();
        var (bob, bobTokens) = await server.GuestAsync();
        var aliceCode = (await alice.GetFromJsonAsync<ProfileResponse>("/v1/profile"))!.FriendCode;
        var bobCode = (await bob.GetFromJsonAsync<ProfileResponse>("/v1/profile"))!.FriendCode;

        await alice.PostAsJsonAsync("/v1/friends/requests", new FriendCodeRequest { Code = bobCode });
        await bob.PostAsJsonAsync("/v1/friends/requests", new FriendCodeRequest { Code = aliceCode });
        Assert.Single((await alice.GetFromJsonAsync<FriendsResponse>("/v1/friends"))!.Friends);

        Assert.Equal(HttpStatusCode.NoContent, (await alice.DeleteAsync($"/v1/friends/{bobTokens.AccountId}")).StatusCode);
        Assert.Empty((await bob.GetFromJsonAsync<FriendsResponse>("/v1/friends"))!.Friends);

        await alice.PostAsJsonAsync("/v1/friends/requests", new FriendCodeRequest { Code = bobCode });
        Assert.Equal(HttpStatusCode.NoContent, (await bob.PostAsync($"/v1/friends/requests/{aliceTokens.AccountId}/decline", null)).StatusCode);
        Assert.Empty((await alice.GetFromJsonAsync<FriendsResponse>("/v1/friends"))!.Outgoing);
    }

    [Fact]
    public async Task Requests_respect_the_targets_setting_and_unknown_codes()
    {
        var (alice, _) = await server.GuestAsync();
        var (bob, _) = await server.GuestAsync();
        var settings = (await bob.GetFromJsonAsync<SettingsResponse>("/v1/settings"))!;
        settings.Settings.FriendReq = false;
        await bob.PutAsJsonAsync("/v1/settings", new SettingsUpdateRequest { Settings = settings.Settings, Revision = settings.Revision });
        var bobCode = (await bob.GetFromJsonAsync<ProfileResponse>("/v1/profile"))!.FriendCode;

        var refused = await alice.PostAsJsonAsync("/v1/friends/requests", new FriendCodeRequest { Code = bobCode });
        Assert.Equal(ErrorCodes.RequestsDisabled, (await refused.Content.ReadFromJsonAsync<ErrorResponse>())!.Code);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.PostAsJsonAsync("/v1/friends/requests", new FriendCodeRequest { Code = "ZZ-0000x" })).StatusCode);
    }

    [Fact]
    public async Task Search_finds_by_code_and_name_prefix()
    {
        var (alice, _) = await server.GuestAsync();
        var (bob, bobTokens) = await server.GuestAsync();
        var unique = "Zebra" + Random.Shared.Next(1000, 9999);
        await bob.PutAsJsonAsync("/v1/profile", new ProfileUpdateRequest { Name = unique, Color = "#3D7BFF", Frame = 0, Status = "" });
        var bobCode = (await bob.GetFromJsonAsync<ProfileResponse>("/v1/profile"))!.FriendCode;

        var byCode = (await alice.GetFromJsonAsync<List<PlayerSearchResponse>>("/v1/players/search?q=" + bobCode))!;
        Assert.Equal(bobTokens.AccountId, Assert.Single(byCode).Player.Id);
        var byName = (await alice.GetFromJsonAsync<List<PlayerSearchResponse>>("/v1/players/search?q=" + unique[..7].ToLowerInvariant()))!;
        Assert.Contains(byName, x => x.Player.Id == bobTokens.AccountId);
        Assert.Equal(HttpStatusCode.BadRequest, (await alice.GetAsync("/v1/players/search?q=ab")).StatusCode);
    }

    [Fact]
    public async Task Share_link_resolves_to_a_public_page_with_escaped_name()
    {
        var (client, _) = await server.GuestAsync();
        await client.PutAsJsonAsync("/v1/profile", new ProfileUpdateRequest { Name = "Ali_ve.Veli", Color = "#3D7BFF", Frame = 0, Status = "" });
        var share = (await client.GetFromJsonAsync<ShareLinkResponse>("/v1/share"))!;
        Assert.EndsWith("/p/" + share.FriendCode, share.Url);
        Assert.Equal("galaxy101://friend/" + share.FriendCode, share.DeepLink);
        var page = await server.CreateClient().GetStringAsync("/p/" + share.FriendCode);
        Assert.Contains("Ali_ve.Veli", page);
        Assert.Equal(HttpStatusCode.NotFound, (await server.CreateClient().GetAsync("/p/QQ-0000")).StatusCode);
    }

    [Fact]
    public async Task Leaderboards_rank_ties_together_and_scope_friends()
    {
        var (alice, aliceTokens) = await server.GuestAsync();
        var (bob, bobTokens) = await server.GuestAsync();
        await MakeFriends(alice, bob);
        await using (var scope = server.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GameDb>();
            var board = LeaderboardEndpoints.SeasonBoard(1);
            db.LeaderboardScores.AddRange(
                new LeaderboardScore { Board = board, AccountId = aliceTokens.AccountId, Points = 5_000_000 },
                new LeaderboardScore { Board = board, AccountId = bobTokens.AccountId, Points = 5_000_000 });
            await db.SaveChangesAsync();
        }
        var season = (await alice.GetFromJsonAsync<LeaderboardResponse>("/v1/leaderboard/season"))!;
        Assert.Equal(1, season.Me.Rank);
        Assert.Equal(season.Rows[0].Rank, season.Rows[1].Rank);

        var friends = (await alice.GetFromJsonAsync<LeaderboardResponse>("/v1/leaderboard/friends"))!;
        Assert.Equal(2, friends.Rows.Count);
        var weekly = (await alice.GetFromJsonAsync<LeaderboardResponse>("/v1/leaderboard/weekly"))!;
        Assert.Equal(0, weekly.Me.Rank);
        Assert.NotNull(weekly.EndsAt);
        Assert.Equal(HttpStatusCode.NotFound, (await alice.GetAsync("/v1/leaderboard/daily")).StatusCode);
    }

    internal static async Task MakeFriends(HttpClient a, HttpClient b)
    {
        var aCode = (await a.GetFromJsonAsync<ProfileResponse>("/v1/profile"))!.FriendCode;
        var bCode = (await b.GetFromJsonAsync<ProfileResponse>("/v1/profile"))!.FriendCode;
        await a.PostAsJsonAsync("/v1/friends/requests", new FriendCodeRequest { Code = bCode });
        await b.PostAsJsonAsync("/v1/friends/requests", new FriendCodeRequest { Code = aCode });
    }
}
