using System.ComponentModel.DataAnnotations;

namespace Common.Infrastructure.Caching;

/// <summary>Settings under <c>Cache</c>. The Valkey connection string is <c>ConnectionStrings:Cache</c>.</summary>
public sealed class CacheOptions
{
    public const string Section = "Cache";

    /// <summary>The connection string name of the optional shared (Valkey/Redis) cache.</summary>
    public const string ConnectionName = "Cache";

    /// <summary>How long an entry lives when the caller does not say (in the shared cache, if there is one).</summary>
    [Range(1, 1440)]
    public int ExpirationMinutes { get; init; } = 5;

    /// <summary>How long an instance keeps an entry in its own memory; shorter than <see cref="ExpirationMinutes"/> so instances see each other's changes.</summary>
    [Range(1, 1440)]
    public int LocalExpirationMinutes { get; init; } = 1;

    /// <summary>The largest entry kept, in bytes; bigger ones are fetched every time.</summary>
    [Range(1024, 104_857_600)]
    public int MaximumPayloadBytes { get; init; } = 1_048_576;

    /// <summary>Prefix for the keys this app writes to a shared cache, so apps sharing one server do not meet.</summary>
    [Required]
    [StringLength(64, MinimumLength = 1)]
    public string KeyPrefix { get; init; } = "app:";
}
