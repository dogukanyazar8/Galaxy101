using System.Security.Cryptography;
using System.Text;
using Galaxy101.Contracts;
using Galaxy101.Server.Auth;
using Galaxy101.Server.Data;
using Galaxy101.Server.Games;
using Galaxy101.Server.Infrastructure;
using Galaxy101.Server.Profile;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Galaxy101.Server.Social;

public sealed class AdminOptions
{
    public const string Section = "Admin";
    /// <summary>Moderator key sent as X-Admin-Key (at least 32 characters); empty disables the admin endpoints (404).</summary>
    public string Key { get; set; } = "";
}

/// <summary>
/// Player safety: blocking (the blocked player's table chat, emotes, friend requests and invitations stop reaching the
/// blocker; a friendship between them ends), reports for moderators, and the moderators' side — open reports and bans.
/// </summary>
public static class ModerationEndpoints
{
    public static readonly string[] Reasons = ["chat", "name", "cheating", "other"];
    public const int ReportsPerDay = 10;

    public static void MapModeration(this IEndpointRouteBuilder app)
    {
        var players = app.MapGroup("/v1/players").RequireAuthorization();

        players.MapGet("/blocks", async (HttpContext http, GameDb db, CancellationToken ct) =>
        {
            var me = http.User.AccountId();
            var ids = await db.Blocks.Where(b => b.BlockerId == me).OrderByDescending(b => b.CreatedAt).Select(b => b.BlockedId).ToListAsync(ct);
            var cards = await Players.SummariesAsync(db, ids, ct);
            return Results.Ok(ids.Where(cards.ContainsKey).Select(id => cards[id]).ToList());
        });

        players.MapPost("/{id:guid}/block", async (Guid id, HttpContext http, GameDb db, MatchManager matches, TimeProvider clock, CancellationToken ct) =>
        {
            var me = http.User.AccountId();
            if (id == me || !await db.Accounts.AnyAsync(a => a.Id == id, ct)) return Api.NotFound();
            var (low, high) = Players.Pair(me, id);
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Friendships.Where(f => f.LowId == low && f.HighId == high).ExecuteDeleteAsync(ct);
            if (!await db.Blocks.AnyAsync(b => b.BlockerId == me && b.BlockedId == id, ct))
            {
                db.Blocks.Add(new PlayerBlock { BlockerId = me, BlockedId = id, CreatedAt = clock.GetUtcNow() });
                try { await db.SaveChangesAsync(ct); }
                catch (DbUpdateException error) when (error.IsUniqueViolation()) { } // a parallel block won
            }
            await tx.CommitAsync(ct);
            await Quietly(matches.BlockChangedAsync(me, id, true));
            return Results.NoContent();
        });

        players.MapDelete("/{id:guid}/block", async (Guid id, HttpContext http, GameDb db, MatchManager matches, CancellationToken ct) =>
        {
            var me = http.User.AccountId();
            await db.Blocks.Where(b => b.BlockerId == me && b.BlockedId == id).ExecuteDeleteAsync(ct);
            await Quietly(matches.BlockChangedAsync(me, id, false));
            return Results.NoContent();
        });

        // One report per player pair a day counts; at most ten a day per reporter.
        players.MapPost("/{id:guid}/report", async (Guid id, ReportRequest request, HttpContext http, GameDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var me = http.User.AccountId();
            var details = (request.Details ?? "").Trim();
            if (id == me || !Reasons.Contains(request.Reason) || details.Length > 200) return Api.Error(ErrorCodes.Validation);
            if (!await db.Accounts.AnyAsync(a => a.Id == id, ct)) return Api.NotFound();
            var since = clock.GetUtcNow().AddDays(-1);
            if (await db.Reports.AnyAsync(r => r.ReporterId == me && r.ReportedId == id && r.CreatedAt > since, ct)) return Results.NoContent();
            if (await db.Reports.CountAsync(r => r.ReporterId == me && r.CreatedAt > since, ct) >= ReportsPerDay) return Api.Error(ErrorCodes.RateLimited, StatusCodes.Status429TooManyRequests);
            db.Reports.Add(new PlayerReport { ReporterId = me, ReportedId = id, Reason = request.Reason!, Details = details, MatchId = request.MatchId, CreatedAt = clock.GetUtcNow() });
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        var admin = app.MapGroup("/v1/admin").AddEndpointFilter(async (context, next) =>
            IsAdmin(context.HttpContext) ? await next(context) : Api.NotFound());

        admin.MapGet("/reports", async (bool? open, GameDb db, CancellationToken ct) =>
        {
            var rows = db.Reports.AsNoTracking();
            if (open != false) rows = rows.Where(r => r.ResolvedAt == null);
            var list = await rows.OrderBy(r => r.CreatedAt).Take(200).ToListAsync(ct);
            var cards = await Players.SummariesAsync(db, [.. list.Select(r => r.ReportedId).Distinct()], ct);
            return Results.Ok(list.Select(r => new ReportResponse
            {
                Id = r.Id, Reporter = r.ReporterId, Reported = cards.GetValueOrDefault(r.ReportedId) ?? new PlayerSummary { Id = r.ReportedId },
                Reason = r.Reason, Details = r.Details, MatchId = r.MatchId, CreatedAt = r.CreatedAt, ResolvedAt = r.ResolvedAt,
            }).ToList());
        });

        admin.MapPost("/reports/{id:long}/resolve", async (long id, GameDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var now = clock.GetUtcNow();
            var done = await db.Reports.Where(r => r.Id == id && r.ResolvedAt == null).ExecuteUpdateAsync(s => s.SetProperty(r => r.ResolvedAt, now), ct);
            return done == 0 ? Api.NotFound() : Results.NoContent();
        });

        // A ban signs the account out everywhere now (sockets, queues, a running match is forfeited) and refuses sign-in
        // and token renewal until it ends; the account and its tokens are kept, so a guest gets back in afterwards.
        admin.MapPost("/accounts/{id:guid}/ban", async (Guid id, BanRequest request, GameDb db, DeletedAccounts denied, IAccountLifecycle lifecycle,
            TimeProvider clock, CancellationToken ct) =>
        {
            if (request.Minutes is < 1 or > 60 * 24 * 365 * 10) return Api.Error(ErrorCodes.Validation);
            var until = clock.GetUtcNow().AddMinutes(request.Minutes);
            var reason = (request.Reason ?? "").Trim();
            var updated = await db.Accounts.Where(a => a.Id == id).ExecuteUpdateAsync(s => s
                .SetProperty(a => a.BannedUntil, until).SetProperty(a => a.BanReason, reason.Length > 200 ? reason[..200] : reason), ct);
            if (updated == 0) return Api.NotFound();
            denied.Add(id);
            await lifecycle.BeforeDeleteAsync(id, ct); // the same release as a deletion: out of queues, rooms and the match
            return Results.NoContent();
        });

        admin.MapPost("/accounts/{id:guid}/unban", async (Guid id, GameDb db, DeletedAccounts denied, CancellationToken ct) =>
        {
            var updated = await db.Accounts.Where(a => a.Id == id).ExecuteUpdateAsync(s => s
                .SetProperty(a => a.BannedUntil, (DateTimeOffset?)null).SetProperty(a => a.BanReason, (string?)null), ct);
            denied.Remove(id);
            return updated == 0 ? Api.NotFound() : Results.NoContent();
        });
    }

    /// <summary>Either side of a block stops friend requests and invitations between the two.</summary>
    public static Task<bool> BlockedAsync(GameDb db, Guid a, Guid b, CancellationToken ct) =>
        db.Blocks.AnyAsync(x => (x.BlockerId == a && x.BlockedId == b) || (x.BlockerId == b && x.BlockedId == a), ct);

    /// <summary>The ban still running for an account, if any.</summary>
    public static async Task<DateTimeOffset?> BannedUntilAsync(GameDb db, Guid account, TimeProvider clock, CancellationToken ct)
    {
        var until = await db.Accounts.Where(a => a.Id == account).Select(a => a.BannedUntil).FirstOrDefaultAsync(ct);
        return until is { } u && u > clock.GetUtcNow() ? u : null;
    }

    public static IResult Banned(DateTimeOffset until, TimeProvider clock) =>
        Results.Json(new LoginErrorResponse { Code = ErrorCodes.Banned, Message = ErrorCodes.Banned, RetryAfter = (int)Math.Ceiling((until - clock.GetUtcNow()).TotalSeconds) },
            statusCode: StatusCodes.Status403Forbidden);

    private static bool IsAdmin(HttpContext http)
    {
        var key = http.RequestServices.GetRequiredService<IOptions<AdminOptions>>().Value.Key;
        if (string.IsNullOrEmpty(key) || key.Length < 32) return false;
        var sent = http.Request.Headers["X-Admin-Key"].ToString();
        return CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(sent)), SHA256.HashData(Encoding.UTF8.GetBytes(key)));
    }

    private static async Task Quietly(Task task)
    {
        try { await task; } catch (InvalidOperationException) { } // the match closed at the same moment
    }
}
