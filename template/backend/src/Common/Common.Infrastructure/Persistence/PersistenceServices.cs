using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.EntityFrameworkCore;

namespace Common.Infrastructure.Persistence;

public static class PersistenceServices
{
    /// <summary>The connection string every module's context uses: <c>ConnectionStrings:Default</c>.</summary>
    public const string ConnectionName = "Default";

    /// <summary>
    /// Registers a module's DbContext on the shared Postgres database, in the module's own
    /// <paramref name="schema"/> (its tables and its migrations history live there), with audit
    /// timestamps, and a readiness check that fails /health/ready when the database is unreachable.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(
        this IServiceCollection services, IConfiguration configuration, string schema)
        where TContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddSingleton<AuditInterceptor>();
        services.AddSingleton(new ModuleDatabase(typeof(TContext)));
        services.AddScoped<ModuleOutbox<TContext>>();
        services.AddDbContextWithWolverineIntegration<TContext>((provider, options) => options
            .UseNpgsql(
                configuration.GetConnectionString(ConnectionName),
                npgsql => npgsql.MigrationsHistoryTable("__ef_migrations", schema))
            .AddInterceptors(provider.GetRequiredService<AuditInterceptor>()));
        services.AddHealthChecks().AddDbContextCheck<TContext>(
            $"database:{schema}", tags: [ReadinessChecks.Tag]);
        return services;
    }
}
