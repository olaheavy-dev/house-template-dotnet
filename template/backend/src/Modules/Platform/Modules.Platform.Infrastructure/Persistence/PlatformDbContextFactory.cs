using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Modules.Platform.Infrastructure.Persistence;

/// <summary>Lets `dotnet ef` build the context without starting the app. It never connects: adding a migration only reads the model.</summary>
internal sealed class PlatformDbContextFactory : IDesignTimeDbContextFactory<PlatformDbContext>
{
    public PlatformDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseNpgsql("Host=localhost", npgsql => npgsql.MigrationsHistoryTable("__ef_migrations", PlatformDbContext.Schema))
            .Options);
}
