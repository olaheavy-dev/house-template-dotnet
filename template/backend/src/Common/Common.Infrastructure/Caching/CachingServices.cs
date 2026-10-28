using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Common.Infrastructure.Caching;

public static class CachingServices
{
    /// <summary>
    /// <see cref="HybridCache"/>: a cache in each instance's memory, in front of an optional shared one. With
    /// <c>ConnectionStrings:Cache</c> set (Valkey, or Redis) instances share entries; without it each instance
    /// has its own memory cache and nothing else is needed. Concurrent requests for the same missing key run the
    /// factory once. A removal clears the shared entry, but there is no backplane: another instance keeps its own
    /// in-memory copy until <see cref="CacheOptions.LocalExpirationMinutes"/> pass, so keep that short for data
    /// that changes. A shared cache that is down costs a read milliseconds and the read goes to the source.
    /// Inject <see cref="HybridCache"/> where it is used.
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
                // A shared cache that is down must not hold requests up: start without it, fail at once while it is
                // unreachable (StackExchange.Redis otherwise queues commands until its once-a-second timeout check,
                // about a second per read), give up quickly on a slow one, and reconnect in the background.
                var connection = ConfigurationOptions.Parse(shared);
                connection.AbortOnConnectFail = false;
                connection.BacklogPolicy = BacklogPolicy.FailFast;
                connection.ConnectRetry = 1;
                connection.ConnectTimeout = options.SharedTimeoutMilliseconds;
                connection.SyncTimeout = options.SharedTimeoutMilliseconds;
                connection.AsyncTimeout = options.SharedTimeoutMilliseconds;
                redis.ConfigurationOptions = connection;
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
