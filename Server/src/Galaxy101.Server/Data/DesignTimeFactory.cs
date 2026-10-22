using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Galaxy101.Server.Data;

/// <summary>Lets <c>dotnet ef migrations add</c> build the model without configuration or a live database.</summary>
public sealed class DesignTimeFactory : IDesignTimeDbContextFactory<GameDb>
{
    public GameDb CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<GameDb>()
        .UseNpgsql("Host=localhost;Database=galaxy101").UseSnakeCaseNamingConvention().Options);
}
