using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Common.Infrastructure.Caching;

public static class CachingServices
{
    /// <summary>
    /// <see cref="HybridCache"/>: a cache in each instance's memory, in front of an optional shared one. With
    /// <c>ConnectionStrings:Cache</c> set (Valkey, or Redis) instances share entries and see each other's
    /// removals; without it each instance has its own memory cache and nothing else is needed. Concurrent
    /// requests for the same missing key run the factory once. Inject <see cref="HybridCache"/> where it is used.
    /// </summary>
    public static IServiceCollection AddCaching(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var options = configuration.GetSection(CacheOptions.Section).Get<CacheOptions>() ?? new CacheOptions();
        services.AddValidatedOptions<CacheOptions>(configuration.GetSection(CacheOptions.Section));

        var shared = configuration.GetConnectionString(CacheOptions.ConnectionName);
        if (!string.IsNullOrWhiteSpace(shared))
        {
            services.AddStackExchangeRedisCache(redis =>
            {
                redis.Configuration = shared;
                redis.InstanceName = options.KeyPrefix;
            });
        }

        services.AddHybridCache(hybrid =>
        {
            hybrid.MaximumPayloadBytes = options.MaximumPayloadBytes;
            hybrid.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(options.ExpirationMinutes),
                LocalCacheExpiration = TimeSpan.FromMinutes(options.LocalExpirationMinutes),
            };
        });
        return services;
    }
}
