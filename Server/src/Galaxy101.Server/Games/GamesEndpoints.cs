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

        app.MapPost("/v1/friends/{id:guid}/invite", async (Guid id, InviteRequest request, HttpContext http, GameDb db,
            PrivateRooms rooms, ConnectionRegistry live, CancellationToken ct) =>
        {
            var me = http.User.AccountId();
            var code = (request.RoomCode ?? "").Trim().ToUpperInvariant();
            if (!rooms.IsMember(me, code) || rooms.StakeOf(code) is not { } stake) return Api.NotFound();
            var (low, high) = Players.Pair(me, id);
            if (!await db.Friendships.AnyAsync(f => f.LowId == low && f.HighId == high && f.Accepted, ct)) return Api.NotFound();
            if (!live.IsOnline(id)) return Api.Conflict(ErrorCodes.NotOnline);
            var settings = await db.Accounts.Where(a => a.Id == id).Select(a => a.SettingsJson).FirstAsync(ct);
            if (!ProfileRules.ReadSettings(settings).Invites) return Api.Conflict(ErrorCodes.InvitesDisabled);
            live.Send(id, MessageTypes.Invite, new InviteMessage { From = await Players.SummaryAsync(db, me, ct), RoomCode = code, Stake = stake });
            return Results.NoContent();
        }).RequireAuthorization();
    }
}

/// <summary>Before an account is deleted: out of queues and rooms, forfeits a running match, socket closed.</summary>
public sealed class LiveAccountLifecycle(Matchmaker queue, PrivateRooms rooms, MatchManager matches, ConnectionRegistry live) : IAccountLifecycle
{
    public async Task BeforeDeleteAsync(Guid account, CancellationToken ct)
    {
        queue.Remove(account);
        rooms.Remove(account);
        await matches.LeaveAsync(account);
        live.Disconnect(account);
    }
}
