using Microsoft.Extensions.DependencyInjection;

namespace Common.Infrastructure.Persistence;

/// <summary>Example data for development. A module registers one; <c>make seed</c> runs them all.</summary>
public interface IDataSeeder
{
    /// <summary>Adds the data if it isn't there yet: running it twice changes nothing.</summary>
    Task SeedAsync(CancellationToken cancellationToken);
}

public static class Seeding
{
    public static async Task RunSeedersAsync(this IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        await using var scope = services.CreateAsyncScope();
        foreach (var seeder in scope.ServiceProvider.GetServices<IDataSeeder>())
        {
            await seeder.SeedAsync(cancellationToken);
        }
    }
}
