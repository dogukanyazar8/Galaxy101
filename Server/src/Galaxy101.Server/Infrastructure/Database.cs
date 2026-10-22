using Npgsql;

namespace Galaxy101.Server.Infrastructure;

public static class Database
{
    /// <summary>
    /// <c>ConnectionStrings:Game</c>, or Railway's <c>DATABASE_URL</c> (<c>postgresql://user:pass@host:port/db</c>).
    /// </summary>
    public static string ConnectionString(IConfiguration configuration)
    {
        if (configuration.GetConnectionString("Game") is { Length: > 0 } direct) return direct;
        var url = configuration["DATABASE_URL"];
        if (string.IsNullOrEmpty(url)) throw new InvalidOperationException("Set ConnectionStrings:Game or DATABASE_URL.");
        var uri = new Uri(url);
        var credentials = uri.UserInfo.Split(':', 2);
        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = uri.AbsolutePath.TrimStart('/'),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : null,
            MaxPoolSize = 50,
        }.ConnectionString;
    }
}
