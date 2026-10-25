using Galaxy101.Contracts;
using Galaxy101.Server.Data;
using Galaxy101.Server.Infrastructure;
using Galaxy101.Server.Profile;
using Galaxy101.Server.Realtime;
using Galaxy101.Server.Social;
using Microsoft.EntityFrameworkCore;

namespace Galaxy101.Server.Games;

public static class GamesEndpoints
{
    public static void MapGames(this IEndpointRouteBuilder app)
    {
        // The menu asks this on start: a match still running means "return to your table" (T26), not a new game.
        app.MapGet("/v1/matches/current", (HttpContext http, MatchManager matches) =>
            matches.Current(http.User.AccountId()) is { } session ? Results.Ok(new MatchRefMessage { MatchId = session.Id }) : Results.NoContent())
            .RequireAuthorization();

        // Invite a friend — or someone you played with recently (spec/20 S15) — to your private room or Klasik table.
        app.MapPost("/v1/friends/{id:guid}/invite", async (Guid id, InviteRequest request, HttpContext http, GameDb db,
            PrivateRooms rooms, Tables tables, ConnectionRegistry live, CancellationToken ct) =>
        {
            var me = http.User.AccountId();
            var code = (request.RoomCode ?? "").Trim().ToUpperInvariant();
            long stake;
            if (tables.Membership(me, code) is { Member: true } table) stake = table.Stake;
            else if (!rooms.IsMember(me, code) || rooms.StakeOf(code) is not { } roomStake) return Api.NotFound();
            else stake = roomStake;
            var (low, high) = Players.Pair(me, id);
            bool friends = await db.Friendships.AnyAsync(f => f.LowId == low && f.HighId == high && f.Accepted, ct);
            if (!friends && !await PlayedTogetherAsync(db, me, id, ct)) return Api.NotFound();
            if (await Social.ModerationEndpoints.BlockedAsync(db, me, id, ct)) return Api.Conflict(ErrorCodes.InvitesDisabled);
            if (!live.IsOnline(id)) return Api.Conflict(ErrorCodes.NotOnline);
            var settings = await db.Accounts.Where(a => a.Id == id).Select(a => a.SettingsJson).FirstAsync(ct);
            if (!ProfileRules.ReadSettings(settings).Invites) return Api.Conflict(ErrorCodes.InvitesDisabled);
            live.Send(id, MessageTypes.Invite, new InviteMessage { From = await Players.SummaryAsync(db, me, ct), RoomCode = code, Stake = stake });
            return Results.NoContent();
        }).RequireAuthorization();

        // Son oynadıkların (S15): people from my last 20 matches, newest first, with their presence.
        app.MapGet("/v1/players/recent", async (HttpContext http, GameDb db, ConnectionRegistry live, CancellationToken ct) =>
        {
            var me = http.User.AccountId();
            var recent = await RecentAsync(db, me, ct);
            var cards = await Players.SummariesAsync(db, recent, ct);
            return Results.Ok(recent.Where(cards.ContainsKey).Select(id => new FriendResponse { Player = cards[id], Presence = live.PresenceOf(id) }).ToList());
        }).RequireAuthorization();
    }

    private static async Task<List<Guid>> RecentAsync(GameDb db, Guid me, CancellationToken ct)
    {
        var mine = db.MatchParticipants.Where(p => p.AccountId == me).OrderByDescending(p => p.FinishedAt).Select(p => p.MatchId).Take(20);
        var others = await db.MatchParticipants.Where(p => mine.Contains(p.MatchId) && p.AccountId != null && p.AccountId != me)
            .OrderByDescending(p => p.FinishedAt).Select(p => new { Id = p.AccountId!.Value, p.FinishedAt }).ToListAsync(ct);
        return [.. others.Select(o => o.Id).Distinct().Take(30)];
    }

    private static async Task<bool> PlayedTogetherAsync(GameDb db, Guid me, Guid other, CancellationToken ct) => (await RecentAsync(db, me, ct)).Contains(other);
}

/// <summary>Before an account is deleted: out of queues and rooms, forfeits a running match, socket closed.</summary>
public sealed class LiveAccountLifecycle(Matchmaker queue, PrivateRooms rooms, Tables tables, MatchManager matches, V4.Hub hub, ConnectionRegistry live) : IAccountLifecycle
{
    public async Task BeforeDeleteAsync(Guid account, CancellationToken ct)
    {
        queue.Remove(account);
        rooms.Remove(account);
        tables.Remove(account, "deleted");
        await matches.LeaveAsync(account);
        await hub.RemoveAsync(account);
        live.Disconnect(account);
    }
}
