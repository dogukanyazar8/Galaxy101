using System.Collections.Concurrent;
using System.Security.Cryptography;
using Galaxy101.Contracts;
using Galaxy101.Server.Data;
using Galaxy101.Server.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Galaxy101.Server.Auth;

public static class AuthEndpoints
{
    public static void MapAuth(this IEndpointRouteBuilder app)
    {
        var anonymous = app.MapGroup("/v1/auth").RequireRateLimiting(RateLimits.Auth);

        anonymous.MapPost("/guest", async (GuestLoginRequest? request, AccountService accounts, TokenService tokens, CancellationToken ct) =>
        {
            var account = await accounts.CreateAsync(request?.Platform, request?.DeviceId, ct);
            return Results.Ok(await tokens.IssueAsync(account.Id, true, ct));
        });

        // Sign in on any device with a linked provider; an unknown identity creates a new linked account.
        anonymous.MapPost("/external", async (ExternalLoginRequest request, IExternalIdentityValidator validator,
            AccountService accounts, TokenService tokens, GameDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var identity = await validator.ValidateAsync(request.Provider, request.IdToken, ct);
            if (identity is null) return Api.Error(ErrorCodes.InvalidToken, StatusCodes.Status401Unauthorized);
            var existing = await db.ExternalLogins.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Provider == identity.Provider && x.Subject == identity.Subject, ct);
            if (existing is not null) return Results.Ok(await tokens.IssueAsync(existing.AccountId, false, ct));
            var account = await accounts.CreateAsync(null, null, ct);
            db.ExternalLogins.Add(new ExternalLogin { Provider = identity.Provider, Subject = identity.Subject, AccountId = account.Id, CreatedAt = clock.GetUtcNow() });
            await db.SaveChangesAsync(ct);
            return Results.Ok(await tokens.IssueAsync(account.Id, true, ct));
        });

        anonymous.MapPost("/refresh", async (RefreshRequest request, TokenService tokens, CancellationToken ct) =>
            await tokens.RefreshAsync(request.RefreshToken, ct) is { } response
                ? Results.Ok(response)
                : Api.Error(ErrorCodes.InvalidToken, StatusCodes.Status401Unauthorized));

        var user = app.MapGroup("/v1/auth").RequireAuthorization();

        // Attach Google / Apple to the signed-in (usually guest) account so it survives reinstalling.
        user.MapPost("/link", async (ExternalLoginRequest request, HttpContext http, IExternalIdentityValidator validator,
            GameDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var identity = await validator.ValidateAsync(request.Provider, request.IdToken, ct);
            if (identity is null) return Api.Error(ErrorCodes.InvalidToken, StatusCodes.Status401Unauthorized);
            var me = http.User.AccountId();
            var owner = await db.ExternalLogins.Where(x => x.Provider == identity.Provider && x.Subject == identity.Subject)
                .Select(x => (Guid?)x.AccountId).FirstOrDefaultAsync(ct);
            if (owner == me) return Results.NoContent();
            if (owner is not null || await db.ExternalLogins.AnyAsync(x => x.AccountId == me && x.Provider == identity.Provider, ct))
                return Api.Conflict(ErrorCodes.ProviderAlreadyLinked);
            db.ExternalLogins.Add(new ExternalLogin { Provider = identity.Provider, Subject = identity.Subject, AccountId = me, CreatedAt = clock.GetUtcNow() });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException error) when (error.IsUniqueViolation()) { return Api.Conflict(ErrorCodes.ProviderAlreadyLinked); }
            return Results.NoContent();
        });

        user.MapPost("/logout", async (RefreshRequest request, HttpContext http, TokenService tokens, CancellationToken ct) =>
        {
            await tokens.RevokeAsync(http.User.AccountId(), request.RefreshToken, ct);
            return Results.NoContent();
        });

        app.MapPost("/v1/realtime/ticket", (HttpContext http, RealtimeTickets tickets) =>
            Results.Ok(new RealtimeTicketResponse { Ticket = tickets.Issue(http.User.AccountId()), ExpiresIn = (int)RealtimeTickets.Lifetime.TotalSeconds }))
            .RequireAuthorization();
    }
}

/// <summary>
/// One-time, 30-second tickets for opening the WebSocket, so access tokens never travel in URLs (and logs).
/// In memory: the game runs as a single instance (see README).
/// </summary>
public sealed class RealtimeTickets(TimeProvider clock)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);
    private readonly ConcurrentDictionary<string, (Guid Account, DateTimeOffset Expires)> tickets = new();

    public string Issue(Guid account)
    {
        var now = clock.GetUtcNow();
        if (tickets.Count > 1024)
            foreach (var (key, value) in tickets) if (value.Expires <= now) tickets.TryRemove(key, out _);
        var ticket = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(24));
        tickets[ticket] = (account, now + Lifetime);
        return ticket;
    }

    public Guid? Redeem(string? ticket) =>
        ticket is not null && tickets.TryRemove(ticket, out var entry) && entry.Expires > clock.GetUtcNow() ? entry.Account : null;
}
