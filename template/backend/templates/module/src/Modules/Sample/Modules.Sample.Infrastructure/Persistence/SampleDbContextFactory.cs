using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Modules.Sample.Infrastructure.Persistence;

/// <summary>Lets `dotnet ef` build the context without starting the app. It never connects: adding a migration only reads the model.</summary>
internal sealed class SampleDbContextFactory : IDesignTimeDbContextFactory<SampleDbContext>
{
    public SampleDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SampleDbContext>()
            .UseNpgsql("Host=localhost", npgsql => npgsql.MigrationsHistoryTable("__ef_migrations", SampleDbContext.Schema))
            .Options);
}
