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
        var anonymous = app.MapGroup("/v1/auth").RequireRateLimiting(RateLimits.Auth).AddEndpointFilter(async (context, next) =>
        {
            try { return await next(context); }
            catch (AccountBannedException banned) { return Social.ModerationEndpoints.Banned(banned.Until, context.HttpContext.RequestServices.GetRequiredService<TimeProvider>()); }
            catch (AccountSessionEndedException) { return Api.Error(ErrorCodes.InvalidToken, StatusCodes.Status401Unauthorized, error: V4Errors.E_SESSION_EXPIRED); }
        });

        // Misafir (spec/18 S05): optional nickname (≤ 16) and avatar colour; guest balance.
        anonymous.MapPost("/guest", async (GuestLoginRequest? request, AccountService accounts, TokenService tokens, CancellationToken ct) =>
        {
            var name = string.IsNullOrWhiteSpace(request?.Name) ? null : request!.Name.Trim();
            if (name is not null && (name.Length > 16 || !Profile.ProfileRules.ValidName(name))) return Api.Error(ErrorCodes.Validation, error: V4Errors.E_NAME_INVALID);
            if (!Social.TextFilter.IsClean(name)) return Api.Error(ErrorCodes.Inappropriate, error: V4Errors.E_NAME_INVALID);
            var color = Profile.ProfileRules.AvatarColors.Contains(request?.Color) ? request!.Color : null;
            var account = await accounts.CreateAsync(request?.Platform, request?.DeviceId, ct, registered: false, name: name, color: color);
            return Results.Ok(await tokens.IssueAsync(account.Id, true, ct));
        });

        // Kayıt ol (spec/18 S04): registered balance.
        anonymous.MapPost("/register", async (RegisterRequest request, Passwords passwords, TokenService tokens, CancellationToken ct) =>
        {
            var outcome = await passwords.RegisterAsync(request, ct);
            return outcome.Account is { } id ? Results.Ok(await tokens.IssueAsync(id, true, ct))
                : outcome.Error == ErrorCodes.EmailTaken ? Api.Conflict(ErrorCodes.EmailTaken, outcome.V4)
                : Api.Error(outcome.Error!, StatusCodes.Status400BadRequest, error: outcome.V4);
        });

        // Giriş yap (spec/18 S03): unknown e-mail 404, wrong password 401 with the tries left, locked 423.
        anonymous.MapPost("/login", async (PasswordLoginRequest request, Passwords passwords, TokenService tokens, TimeProvider clock, CancellationToken ct) =>
        {
            var outcome = await passwords.LoginAsync(request, ct);
            if (outcome.Account is { } id) return Results.Ok(await tokens.IssueAsync(id, false, ct, outcome.SessionVersion));
            int status = outcome.Error == ErrorCodes.AccountNotFound ? StatusCodes.Status404NotFound
                : outcome.Error == ErrorCodes.AccountLocked ? StatusCodes.Status423Locked : StatusCodes.Status401Unauthorized;
            object? detail = outcome.V4 == V4Errors.E_PASS_WRONG ? new { left = outcome.Remaining }
                : outcome.V4 == V4Errors.E_ACCOUNT_LOCKED ? new { until = clock.GetUtcNow().AddSeconds(outcome.RetryAfter).ToUnixTimeMilliseconds() } : null;
            return Results.Json(new LoginErrorResponse
            {
                Code = outcome.Error, Message = outcome.Error, RemainingAttempts = outcome.Remaining, RetryAfter = outcome.RetryAfter, Error = outcome.V4, Detail = detail,
            }, statusCode: status);
        });

        // Sign in on any device with a linked provider; an unknown identity creates a new linked account.
        anonymous.MapPost("/external", async (ExternalLoginRequest request, IExternalIdentityValidator validator,
            AccountService accounts, TokenService tokens, GameDb db, TimeProvider clock, CancellationToken ct) =>
        {
            var identity = await validator.ValidateAsync(request.Provider, request.IdToken, ct);
            if (identity is null) return Api.Error(ErrorCodes.InvalidToken, StatusCodes.Status401Unauthorized, error: V4Errors.E_OAUTH_FAILED);
            return Results.Ok(await SignInAsync(identity, accounts, tokens, db, clock, ct));
        });

        // Which providers this server is set up for: the game shows Google's page only when it can finish the sign-in.
        anonymous.MapGet("/providers", (Microsoft.Extensions.Options.IOptions<AuthOptions> auth) =>
            Results.Ok(new AuthProvidersResponse { GoogleDesktopClientId = auth.Value.GoogleDesktopEnabled ? auth.Value.GoogleDesktopClientId : null }));

        // Google ile devam et (S02) on desktop: the game caught Google's code on a loopback redirect (RFC 8252) with a PKCE
        // verifier (RFC 7636); the server exchanges it with the desktop client's secret and verifies the id token.
        anonymous.MapPost("/google/code", async (GoogleCodeRequest request, IGoogleCodeExchange exchange, IExternalIdentityValidator validator,
            Microsoft.Extensions.Options.IOptions<AuthOptions> auth, AccountService accounts, TokenService tokens, GameDb db, TimeProvider clock, CancellationToken ct) =>
        {
            if (!auth.Value.GoogleDesktopEnabled) return Api.Error(ErrorCodes.ProviderDisabled, StatusCodes.Status503ServiceUnavailable, error: V4Errors.E_OAUTH_FAILED);
            if (string.IsNullOrEmpty(request.Code) || request.Code.Length > 2048 || !GoogleCodeExchange.IsLoopbackRedirect(request.RedirectUri)
                || request.CodeVerifier is not { Length: >= 43 and <= 128 }) return Api.Error(ErrorCodes.Validation, error: V4Errors.E_OAUTH_FAILED);
            string? idToken;
            try { idToken = await exchange.ExchangeAsync(request.Code, request.CodeVerifier, request.RedirectUri!, ct); }
            catch (ExternalProviderUnavailableException) { return Api.Error(ErrorCodes.ProviderUnavailable, StatusCodes.Status503ServiceUnavailable, error: V4Errors.E_OAUTH_FAILED); }
            var identity = idToken is null ? null : await validator.ValidateAsync("google", idToken, ct);
            if (identity is null) return Api.Error(ErrorCodes.InvalidToken, StatusCodes.Status401Unauthorized, error: V4Errors.E_OAUTH_FAILED);
            return Results.Ok(await SignInAsync(identity, accounts, tokens, db, clock, ct));
        });

        anonymous.MapPasswordReset();

        anonymous.MapPost("/refresh", async (RefreshRequest request, TokenService tokens, CancellationToken ct) =>
            await tokens.RefreshAsync(request.RefreshToken, ct) is { } response
                ? Results.Ok(response)
                : Api.Error(ErrorCodes.InvalidToken, StatusCodes.Status401Unauthorized, error: V4Errors.E_SESSION_EXPIRED));

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

        app.MapPost("/v1/realtime/ticket", async (HttpContext http, RealtimeTickets tickets, GameDb db, CancellationToken ct) =>
        {
            var me = http.User.AccountId();
            if (!TokenService.TrySessionVersion(http.User, out var version) ||
                !await db.Accounts.AnyAsync(a => a.Id == me && a.SessionVersion == version, ct)) return Results.Unauthorized();
            return Results.Ok(new RealtimeTicketResponse { Ticket = tickets.Issue(me, version), ExpiresIn = (int)RealtimeTickets.Lifetime.TotalSeconds });
        }).RequireAuthorization();
    }

    /// <summary>
    /// The account a verified provider identity signs into; an unknown identity gets a new registered account (R-96:
    /// 25.000 chips) named after the provider's first name when that is a valid, clean game name.
    /// </summary>
    private static async Task<TokenResponse> SignInAsync(ExternalIdentity identity, AccountService accounts, TokenService tokens, GameDb db,
        TimeProvider clock, CancellationToken ct)
    {
        var existing = await db.ExternalLogins.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Provider == identity.Provider && x.Subject == identity.Subject, ct);
        if (existing is not null) return await tokens.IssueAsync(existing.AccountId, false, ct);
        var name = identity.Name?.Trim();
        if (name is not null && (name.Length > 16 || !Profile.ProfileRules.ValidName(name) || !Social.TextFilter.IsClean(name))) name = null;
        var account = await accounts.CreateAsync(null, null, ct, registered: true, name: name);
        db.ExternalLogins.Add(new ExternalLogin { Provider = identity.Provider, Subject = identity.Subject, AccountId = account.Id, CreatedAt = clock.GetUtcNow() });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException error) when (error.IsUniqueViolation())
        {
            // The same identity signed in twice at once: the other request created the link; use its account.
            db.ChangeTracker.Clear();
            var winner = await db.ExternalLogins.AsNoTracking().FirstAsync(x => x.Provider == identity.Provider && x.Subject == identity.Subject, ct);
            return await tokens.IssueAsync(winner.AccountId, false, ct);
        }
        return await tokens.IssueAsync(account.Id, true, ct);
    }
}

/// <summary>
/// Accounts deleted while access tokens issued to them may still be unexpired: the bearer check refuses them. Held for one
/// token lifetime, in memory like the tickets (single instance); after a restart the ticket's account check still applies.
/// </summary>
public sealed class DeletedAccounts(TimeProvider clock, Microsoft.Extensions.Options.IOptions<AuthOptions> auth)
{
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> until = new();

    public void Add(Guid account)
    {
        var now = clock.GetUtcNow();
        foreach (var (id, expiry) in until) if (expiry <= now) until.TryRemove(id, out _);
        until[account] = now.AddMinutes(auth.Value.AccessTokenMinutes + 1);
    }

    public void Remove(Guid account) => until.TryRemove(account, out _);

    public bool Contains(Guid account) => until.TryGetValue(account, out var expiry) && expiry > clock.GetUtcNow();
}

/// <summary>
/// One-time, 30-second tickets for opening the WebSocket, so access tokens never travel in URLs (and logs).
/// In memory: the game runs as a single instance (see README).
/// </summary>
public sealed class RealtimeTickets(TimeProvider clock)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(30);
    public readonly record struct Session(Guid Account, long Version);
    private readonly ConcurrentDictionary<string, (Session Session, DateTimeOffset Expires)> tickets = new();

    public string Issue(Guid account, long sessionVersion)
    {
        var now = clock.GetUtcNow();
        if (tickets.Count > 1024)
            foreach (var (key, value) in tickets) if (value.Expires <= now) tickets.TryRemove(key, out _);
        var ticket = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(24));
        tickets[ticket] = (new Session(account, sessionVersion), now + Lifetime);
        return ticket;
    }

    public Session? Redeem(string? ticket) =>
        ticket is not null && tickets.TryRemove(ticket, out var entry) && entry.Expires > clock.GetUtcNow() ? entry.Session : null;
}
