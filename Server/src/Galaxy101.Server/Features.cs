using Galaxy101.Server.Economy;
using Galaxy101.Server.Profile;
using Galaxy101.Server.Realtime;
using Galaxy101.Server.Social;

namespace Galaxy101.Server;

/// <summary>Registration of the menu and game features, in plan order (S2–S8).</summary>
public static class Features
{
    public static IServiceCollection AddGameFeatures(this IServiceCollection services)
    {
        services.AddSingleton<IAccountLifecycle, NoLiveState>();
        services.AddSingleton<Catalog>();
        services.AddScoped<DailyService>();
        services.AddHttpClient(GooglePlayVerifier.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(10));
        services.AddSingleton<GooglePlayVerifier>();
        services.AddSingleton<AppleStoreVerifier>();
        services.AddScoped<IStoreVerifier, StoreVerifier>();
        services.AddSingleton<ConnectionRegistry>();
        return services;
    }

    public static IEndpointRouteBuilder MapGameFeatures(this IEndpointRouteBuilder app)
    {
        app.MapProfile();
        app.MapShop();
        app.MapDaily();
        app.MapRealtime();
        app.MapFriends();
        app.MapLeaderboard();
        return app;
    }

    private sealed class NoLiveState : IAccountLifecycle
    {
        public Task BeforeDeleteAsync(Guid account, CancellationToken ct) => Task.CompletedTask;
    }
}
