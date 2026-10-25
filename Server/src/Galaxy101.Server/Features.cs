using Galaxy101.Server.Economy;
using Galaxy101.Server.Games;
using Galaxy101.Server.Profile;
using Galaxy101.Server.Realtime;
using Galaxy101.Server.Social;

namespace Galaxy101.Server;

/// <summary>Registration of the menu and game features, in plan order (S2–S8).</summary>
public static class Features
{
    public static IServiceCollection AddGameFeatures(this IServiceCollection services)
    {
        // S3–S5 economy
        services.AddSingleton<Catalog>();
        services.AddScoped<DailyService>();
        services.AddHttpClient(GooglePlayVerifier.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(10));
        services.AddSingleton<GooglePlayVerifier>();
        services.AddSingleton<AppleStoreVerifier>();
        services.AddScoped<IStoreVerifier, StoreVerifier>();

        // S6 realtime; S8 online tables (one instance each, shared by the socket router and the endpoints)
        services.AddSingleton<ConnectionRegistry>();
        services.AddSingleton<Lobby>();
        services.AddSingleton<Entry>();
        services.AddSingleton<MatchStore>();
        services.AddSingleton<MatchManager>();
        services.AddSingleton<Matchmaker>();
        services.AddSingleton<PrivateRooms>();
        services.AddSingleton<Tables>();
        services.AddSingleton<IRealtimeHandler>(sp => sp.GetRequiredService<Tables>());
        services.AddSingleton<IRealtimeHandler>(sp => sp.GetRequiredService<MatchManager>());
        services.AddSingleton<IRealtimeHandler>(sp => sp.GetRequiredService<Matchmaker>());
        services.AddSingleton<IRealtimeHandler>(sp => sp.GetRequiredService<PrivateRooms>());
        services.AddHostedService(sp => sp.GetRequiredService<MatchManager>());
        services.AddSingleton<IAccountLifecycle, LiveAccountLifecycle>();
        return services;
    }

    public static IEndpointRouteBuilder MapGameFeatures(this IEndpointRouteBuilder app)
    {
        app.MapProfile();
        app.MapShop();
        app.MapDaily();
        app.MapRealtime();
        app.MapFriends();
        app.MapModeration();
        app.MapLeaderboard();
        app.MapGames();
        return app;
    }
}
