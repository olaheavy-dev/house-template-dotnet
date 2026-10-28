using Common.Infrastructure.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Persistence.Durability;
using Wolverine.Runtime;

namespace Common.Infrastructure.Persistence;

public static class DatabaseMigration
{
    /// <summary>
    /// Brings the whole database up to date: every module's tables (their EF Core migrations) and the
    /// messaging library's own (outbox, inbox, dead letters in the <see cref="MessagingServices.Schema"/> schema).
    /// A deploy runs it once before the new version starts (<c>Api.dll migrate</c>), and the API itself never
    /// changes the schema outside Development. Safe to repeat.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        await services.MigrateModuleDatabasesAsync(cancellationToken);
        if (services.GetRequiredService<IWolverineRuntime>().Storage is { } storage and not NullMessageStore)
        {
            await storage.Admin.MigrateAsync();
        }
    }
}
