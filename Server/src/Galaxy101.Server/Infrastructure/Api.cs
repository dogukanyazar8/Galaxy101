using System.Security.Claims;
using Galaxy101.Contracts;
using Microsoft.IdentityModel.JsonWebTokens;
using Npgsql;

namespace Galaxy101.Server.Infrastructure;

public static class Api
{
    /// <param name="error">The v4 protocol error code of this failure (docs/v4/PROTOKOL_ESLEME.md).</param>
    public static IResult Error(string code, int status = StatusCodes.Status400BadRequest, string? message = null, string? error = null, object? detail = null) =>
        Results.Json(new ErrorResponse { Code = code, Message = message ?? code, Error = error, Detail = detail }, statusCode: status);

    public static IResult NotFound(string? error = null) => Error(ErrorCodes.NotFound, StatusCodes.Status404NotFound, error: error);
    public static IResult Conflict(string code = ErrorCodes.Conflict, string? error = null, object? detail = null) =>
        Error(code, StatusCodes.Status409Conflict, error: error, detail: detail);

    public static Guid AccountId(this ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? throw new InvalidOperationException("Unauthenticated principal."));

    public static bool IsUniqueViolation(this Exception error) =>
        error.GetBaseException() is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}

/// <summary>Day and week boundaries for rewards and leaderboards, in the game's home time zone.</summary>
public sealed class GameCalendar(TimeProvider clock, Microsoft.Extensions.Options.IOptions<GameOptions> options)
{
    private readonly TimeZoneInfo zone = Resolve(options.Value.TimeZone);

    public DateTimeOffset Now => clock.GetUtcNow();
    public DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(Now, zone).DateTime);
    public DateOnly WeekStart(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
    public string WeeklyBoard => "w:" + WeekStart(Today).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
    public DateTimeOffset StartOf(DateOnly day)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }
    public DateTimeOffset NextReset => StartOf(Today.AddDays(1));
    public DateTimeOffset WeekEnd => StartOf(WeekStart(Today).AddDays(7));
    /// <summary>The Dereceli season of today (1 before the first season starts).</summary>
    public int Season
    {
        get
        {
            var o = options.Value; var days = Today.DayNumber - o.SeasonStart.DayNumber;
            return days < 0 ? 1 : 1 + days / (7 * Math.Max(1, o.SeasonWeeks));
        }
    }
    public DateTimeOffset SeasonEnd => StartOf(options.Value.SeasonStart.AddDays(7 * Math.Max(1, options.Value.SeasonWeeks) * Season));

    private static TimeZoneInfo Resolve(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        // Turkey has kept UTC+3 all year since 2016; slim containers may lack tzdata.
        catch (TimeZoneNotFoundException) { return TimeZoneInfo.CreateCustomTimeZone(id, TimeSpan.FromHours(3), id, id); }
    }
}
