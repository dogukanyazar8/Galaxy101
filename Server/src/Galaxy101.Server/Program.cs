using System.Text.Json.Serialization;
using Galaxy101.Contracts;
using Galaxy101.Server;
using Galaxy101.Server.Auth;
using Galaxy101.Server.Data;
using Galaxy101.Server.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;
var configuration = builder.Configuration;

services.AddOptions<AuthOptions>().Bind(configuration.GetSection(AuthOptions.Section))
    .Validate(o => { try { return TokenService.SigningKey(o) is not null; } catch { return false; } }, "Auth:SigningKey must be base64 of at least 32 bytes.")
    .ValidateOnStart();
services.Configure<StoreOptions>(configuration.GetSection(StoreOptions.Section));
services.Configure<GameOptions>(configuration.GetSection(GameOptions.Section));

services.AddDbContextPool<GameDb>(o => o.UseNpgsql(Database.ConnectionString(configuration)).UseSnakeCaseNamingConvention());
services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase)));

services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<IOptions<AuthOptions>, TimeProvider>((jwt, auth, clock) =>
{
    jwt.MapInboundClaims = false;
    jwt.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = auth.Value.Issuer,
        ValidAudience = auth.Value.Audience,
        IssuerSigningKey = TokenService.SigningKey(auth.Value),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        // Same clock that issued the token (testable, and one notion of "now" across the server).
        LifetimeValidator = (notBefore, expires, _, _) =>
        {
            var now = clock.GetUtcNow().UtcDateTime;
            return (notBefore is null || notBefore <= now.AddSeconds(30)) && expires is not null && expires > now.AddSeconds(-30);
        },
    };
});
services.AddAuthorization();
services.AddGameRateLimits(configuration);
services.Configure<ForwardedHeadersOptions>(o =>
{
    // Railway terminates TLS at its edge proxy; trust exactly one hop of X-Forwarded-*.
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.ForwardLimit = 1;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

services.AddSingleton(TimeProvider.System);
services.AddSingleton<GameCalendar>();
services.AddSingleton<RealtimeTickets>();
services.AddSingleton<IExternalIdentityValidator, OidcIdentityValidator>();
services.AddScoped<TokenService>();
services.AddScoped<AccountService>();
services.AddScoped<Galaxy101.Server.Economy.Ledger>();
services.AddGameFeatures();

var app = builder.Build();

if (configuration.GetValue("Database:MigrateOnStartup", true))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<GameDb>().Database.MigrateAsync();
}

app.UseForwardedHeaders();
app.UseExceptionHandler(e => e.Run(http => http.Response.WriteAsJsonAsync(new ErrorResponse { Code = "server_error", Message = "server_error" })));
app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();

app.MapGet("/health", async (GameDb db, CancellationToken ct) =>
    await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "ok" }) : Results.StatusCode(StatusCodes.Status503ServiceUnavailable))
    .DisableRateLimiting();
app.MapGet("/v1/info", (GameCalendar calendar, IOptions<GameOptions> game) => new ServerInfoResponse
{
    Version = typeof(Program).Assembly.GetName().Version?.ToString(3),
    RuleVersion = "101-v1",
    ServerTime = calendar.Now,
    PrivacyPolicyVersion = game.Value.PrivacyPolicyVersion,
    PrivacyPolicyUrl = game.Value.PrivacyPolicyUrl,
});
app.MapAuth();
app.MapGameFeatures();

app.Run();

public partial class Program;
