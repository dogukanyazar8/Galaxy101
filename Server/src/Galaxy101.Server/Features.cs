namespace Galaxy101.Server;

/// <summary>Registration of the menu and game features, one line each, in plan order (S2–S8).</summary>
public static class Features
{
    public static IServiceCollection AddGameFeatures(this IServiceCollection services)
    {
        return services;
    }

    public static IEndpointRouteBuilder MapGameFeatures(this IEndpointRouteBuilder app)
    {
        return app;
    }
}
