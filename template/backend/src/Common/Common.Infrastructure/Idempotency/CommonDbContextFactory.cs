using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Common.Infrastructure.Idempotency;

/// <summary>Lets `dotnet ef` build the context without starting the app. It never connects: adding a migration only reads the model.</summary>
internal sealed class CommonDbContextFactory : IDesignTimeDbContextFactory<CommonDbContext>
{
    public CommonDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<CommonDbContext>()
            .UseNpgsql("Host=localhost", npgsql => npgsql.MigrationsHistoryTable("__ef_migrations", CommonDbContext.Schema))
            .Options);
}
