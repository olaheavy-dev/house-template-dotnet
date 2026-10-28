using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Common.Infrastructure.Persistence;

/// <summary>A database context a module registered with <see cref="PersistenceServices.AddModuleDbContext{TContext}"/>.</summary>
public sealed record ModuleDatabase(Type ContextType);

public static class ModuleDatabases
{
    /// <summary>
    /// Applies every module's migrations to the database in <c>ConnectionStrings:Default</c>. For tests
    /// and local runs that start the whole host; a deploy runs the bundles from <c>make migrate-bundle</c>.
    /// </summary>
    public static async Task MigrateModuleDatabasesAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        await using var scope = services.CreateAsyncScope();
        foreach (var database in scope.ServiceProvider.GetServices<ModuleDatabase>())
        {
            var context = (DbContext)scope.ServiceProvider.GetRequiredService(database.ContextType);
            await context.Database.MigrateAsync(cancellationToken);
        }
    }
}
