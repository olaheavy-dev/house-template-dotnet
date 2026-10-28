using Common.Application.Idempotency;
using Common.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Common.Infrastructure.Idempotency;

public static class IdempotencyServices
{
    /// <summary>
    /// Remembers <c>Idempotency-Key</c> requests in the shared kernel's <c>common</c> schema. Endpoints opt in with
    /// <c>.WithIdempotencyKey()</c> (<c>Common.Presentation</c>) and the host adds <c>app.UseIdempotency()</c>.
    /// </summary>
    public static IServiceCollection AddIdempotency(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddValidatedOptions<IdempotencyOptions>(configuration.GetSection(IdempotencyOptions.Section));
        services.AddModuleDbContext<CommonDbContext>(configuration, CommonDbContext.Schema);
        services.AddScoped<IIdempotencyStore, PostgresIdempotencyStore>();
        return services;
    }
}
