using System.Threading.RateLimiting;
using Galaxy101.Contracts;
using Microsoft.AspNetCore.RateLimiting;

namespace Galaxy101.Server.Infrastructure;

public static class RateLimits
{
    public const string Auth = "auth";

    public static IServiceCollection AddGameRateLimits(this IServiceCollection services) => services.AddRateLimiter(o =>
    {
        o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        o.OnRejected = (context, ct) => new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(
            new ErrorResponse { Code = ErrorCodes.RateLimited, Message = ErrorCodes.RateLimited }, ct));
        // Sign-in creates accounts: tight per client address.
        o.AddPolicy(Auth, http => RateLimitPartition.GetFixedWindowLimiter(Client(http),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
        // Everything else: a generous token bucket per account (or address before sign-in).
        o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(http => RateLimitPartition.GetTokenBucketLimiter(
            http.User.Identity?.IsAuthenticated == true ? "a:" + http.User.AccountId() : "ip:" + Client(http),
            _ => new TokenBucketRateLimiterOptions { TokenLimit = 60, TokensPerPeriod = 20, ReplenishmentPeriod = TimeSpan.FromSeconds(10), QueueLimit = 0 }));
    });

    private static string Client(HttpContext http) => http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
