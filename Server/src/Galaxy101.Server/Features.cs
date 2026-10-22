using Galaxy101.Server.Economy;
using Galaxy101.Server.Profile;

namespace Galaxy101.Server;

/// <summary>Registration of the menu and game features, in plan order (S2–S8).</summary>
public static class Features
{
    public static IServiceCollection AddGameFeatures(this IServiceCollection services)
    {
        services.AddSingleton<IAccountLifecycle, NoLiveState>();
        services.AddSingleton<Catalog>();
        services.AddScoped<DailyService>();
        return services;
    }

    public static IEndpointRouteBuilder MapGameFeatures(this IEndpointRouteBuilder app)
    {
        app.MapProfile();
        app.MapShop();
        app.MapDaily();
        return app;
    }

    private sealed class NoLiveState : IAccountLifecycle
    {
        public Task BeforeDeleteAsync(Guid account, CancellationToken ct) => Task.CompletedTask;
    }
}
