using Galaxy101.Contracts;
using Galaxy101.Server.Data;
using Galaxy101.Server.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Galaxy101.Server.Social;

/// <summary>
/// Weekly points (all rated online modes) and season LP (ranked), both written only by server-settled matches.
/// Rank = 1 + number of strictly higher scores, so ties share a rank; served from the (board, points desc) index.
/// </summary>
public static class LeaderboardEndpoints
{
    private const int Top = 50;

    public static string SeasonBoard(int season) => "s:" + season;

    public static void MapLeaderboard(this IEndpointRouteBuilder app) =>
        app.MapGet("/v1/leaderboard/{scope}", async (string scope, HttpContext http, GameDb db, GameCalendar calendar, IOptions<GameOptions> game, CancellationToken ct) =>
        {
            var me = http.User.AccountId();
            var (board, endsAt) = scope switch
            {
                "weekly" => (calendar.WeeklyBoard, (DateTimeOffset?)calendar.WeekEnd),
                "season" or "friends" => (SeasonBoard(calendar.Season), (DateTimeOffset?)calendar.SeasonEnd),
                _ => (null, null),
            };
            if (board is null) return Api.NotFound();

            var scores = db.LeaderboardScores.AsNoTracking().Where(x => x.Board == board);
            if (scope == "friends")
            {
                var circle = db.Friendships.Where(f => f.Accepted && (f.LowId == me || f.HighId == me))
                    .Select(f => f.LowId == me ? f.HighId : f.LowId);
                scores = scores.Where(x => x.AccountId == me || circle.Contains(x.AccountId));
            }
            var top = await scores.OrderByDescending(x => x.Points).ThenBy(x => x.AccountId).Take(Top).Select(x => new { x.AccountId, x.Points }).ToListAsync(ct);
            var mine = await scores.Where(x => x.AccountId == me).Select(x => (long?)x.Points).FirstOrDefaultAsync(ct);
            var myRank = mine is { } points ? 1 + await scores.CountAsync(x => x.Points > points, ct) : 0;

            var cards = await Players.SummariesAsync(db, [.. top.Select(x => x.AccountId).Append(me)], ct, calendar.Season);
            var response = new LeaderboardResponse { Scope = scope, EndsAt = endsAt };
            int rank = 0; long? previous = null;
            for (var i = 0; i < top.Count; i++)
            {
                if (top[i].Points != previous) { rank = i + 1; previous = top[i].Points; }
                if (cards.TryGetValue(top[i].AccountId, out var card))
                    response.Rows.Add(new LeaderboardRow { Rank = rank, Player = card, Points = top[i].Points, Me = top[i].AccountId == me });
            }
            response.Me = new LeaderboardRow { Rank = myRank, Player = cards.GetValueOrDefault(me), Points = mine ?? 0, Me = true };
            return Results.Ok(response);
        }).RequireAuthorization();
}
