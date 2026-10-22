using System.Text.Json;
using Galaxy101.Contracts;
using Galaxy101.Server.Data;
using Galaxy101.Server.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Galaxy101.Server.Profile;

public static class ProfileEndpoints
{
    public static void MapProfile(this IEndpointRouteBuilder app)
    {
        var profile = app.MapGroup("/v1/profile").RequireAuthorization();

        profile.MapGet("", async (HttpContext http, GameDb db, CancellationToken ct) =>
            await Describe(db, http.User.AccountId(), ct) is { } response ? Results.Ok(response) : Api.NotFound());

        profile.MapPut("", async (ProfileUpdateRequest request, HttpContext http, GameDb db, CancellationToken ct) =>
        {
            if (!ProfileRules.ValidProfile(request)) return Api.Error(ErrorCodes.Validation);
            var me = http.User.AccountId();
            var updated = await db.Accounts.Where(x => x.Id == me).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Name, request.Name.Trim())
                .SetProperty(x => x.Color, request.Color)
                .SetProperty(x => x.Status, request.Status ?? "")
                .SetProperty(x => x.Frame, request.Frame), ct);
            return updated == 0 ? Api.NotFound() : Results.Ok(await Describe(db, me, ct));
        });

        profile.MapGet("/stats", async (HttpContext http, GameDb db, CancellationToken ct) =>
        {
            var s = await db.Stats.AsNoTracking().FirstAsync(x => x.AccountId == http.User.AccountId(), ct);
            return Results.Ok(new StatsResponse
            {
                Hands = s.Hands, Wins = s.Wins, WinRate = Percent(s.Wins, s.Hands), BestOpening = s.BestOpening,
                OkeyFinishes = s.OkeyFinishes, BestStreak = s.BestStreak,
                ModeWinRates = [.. s.ModeHands.Select((hands, i) => Percent(s.ModeWins[i], hands))],
            });
        });

        profile.MapGet("/badges", async (HttpContext http, GameDb db, CancellationToken ct) =>
        {
            var me = http.User.AccountId();
            var stats = await db.Stats.AsNoTracking().FirstAsync(x => x.AccountId == me, ct);
            var planets = await db.Accounts.Where(x => x.Id == me).Select(x => x.PlanetsTried).FirstAsync(ct);
            return Results.Ok(Badges.Evaluate(stats, planets));
        });

        profile.MapGet("/history", async (HttpContext http, GameDb db, CancellationToken ct) =>
        {
            var me = http.User.AccountId();
            var rows = await db.MatchParticipants.AsNoTracking().Where(x => x.AccountId == me)
                .OrderByDescending(x => x.FinishedAt).Take(20)
                .Select(x => new HistoryEntryResponse { MatchId = x.MatchId, Mode = x.Mode, Place = x.Place, ChipDelta = x.ChipDelta, LpDelta = x.LpDelta, FinishedAt = x.FinishedAt })
                .ToListAsync(ct);
            return Results.Ok(rows);
        });

        // KVKK: records which version of the privacy notice the player accepted.
        profile.MapPost("/privacy", async (PrivacyConsentRequest request, HttpContext http, GameDb db, IOptions<GameOptions> game, CancellationToken ct) =>
        {
            if (request.Version != game.Value.PrivacyPolicyVersion) return Api.Error(ErrorCodes.Validation);
            var me = http.User.AccountId();
            await db.Accounts.Where(x => x.Id == me).ExecuteUpdateAsync(s => s.SetProperty(x => x.AcceptedPrivacyVersion, request.Version), ct);
            return Results.NoContent();
        });

        var settings = app.MapGroup("/v1/settings").RequireAuthorization();

        settings.MapGet("", async (HttpContext http, GameDb db, CancellationToken ct) =>
        {
            var a = await db.Accounts.AsNoTracking().Where(x => x.Id == http.User.AccountId())
                .Select(x => new { x.SettingsJson, x.SettingsRevision, x.Planet }).FirstAsync(ct);
            return Results.Ok(new SettingsResponse { Settings = ProfileRules.ReadSettings(a.SettingsJson), Planet = a.Planet, Revision = a.SettingsRevision });
        });

        // Last writer loses: a device saving over a newer revision gets 409 and reloads.
        settings.MapPut("", async (SettingsUpdateRequest request, HttpContext http, GameDb db, CancellationToken ct) =>
        {
            if (!ProfileRules.ValidSettings(request.Settings)) return Api.Error(ErrorCodes.Validation);
            var me = http.User.AccountId();
            var json = JsonSerializer.Serialize(request.Settings, ProfileRules.Json);
            var updated = await db.Accounts.Where(x => x.Id == me && x.SettingsRevision == request.Revision)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.SettingsJson, json).SetProperty(x => x.SettingsRevision, x => x.SettingsRevision + 1), ct);
            if (updated == 0) return Api.Conflict();
            return Results.Ok(new SettingsResponse { Settings = request.Settings, Revision = request.Revision + 1,
                Planet = await db.Accounts.Where(x => x.Id == me).Select(x => x.Planet).FirstAsync(ct) });
        });

        settings.MapPut("/planet", async (PlanetUpdateRequest request, HttpContext http, GameDb db, CancellationToken ct) =>
        {
            var bit = ProfileRules.PlanetBit(request.Planet);
            if (bit == 0) return Api.Error(ErrorCodes.Validation);
            var me = http.User.AccountId();
            await db.Accounts.Where(x => x.Id == me).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Planet, request.Planet).SetProperty(x => x.PlanetsTried, x => x.PlanetsTried | bit), ct);
            return Results.NoContent();
        });

        // Store requirement: in-app deletion. Personal data goes; purchase and match records stay anonymised.
        app.MapDelete("/v1/account", async (HttpContext http, GameDb db, IAccountLifecycle lifecycle, CancellationToken ct) =>
        {
            var me = http.User.AccountId();
            await lifecycle.BeforeDeleteAsync(me, ct);
            var deleted = await db.Accounts.Where(x => x.Id == me).ExecuteDeleteAsync(ct);
            return deleted == 0 ? Api.NotFound() : Results.NoContent();
        }).RequireAuthorization();
    }

    public static async Task<ProfileResponse?> Describe(GameDb db, Guid id, CancellationToken ct)
    {
        var a = await db.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return null;
        var s = await db.Stats.AsNoTracking().Where(x => x.AccountId == id).Select(x => new { x.Xp, x.Lp }).FirstAsync(ct);
        var providers = await db.ExternalLogins.Where(x => x.AccountId == id).Select(x => x.Provider).ToListAsync(ct);
        return new ProfileResponse
        {
            Id = a.Id, Name = a.Name, Color = a.Color, Status = a.Status, Frame = a.Frame, FriendCode = a.FriendCode,
            Planet = a.Planet, Progress = Progression.Describe(s.Xp, s.Lp), LinkedProviders = providers,
            AcceptedPrivacyVersion = a.AcceptedPrivacyVersion,
        };
    }

    private static int Percent(int part, int whole) => whole == 0 ? 0 : (int)Math.Round(100.0 * part / whole);
}

/// <summary>Lets live features (sockets, queues, matches) release an account before its rows are deleted.</summary>
public interface IAccountLifecycle
{
    Task BeforeDeleteAsync(Guid account, CancellationToken ct);
}
