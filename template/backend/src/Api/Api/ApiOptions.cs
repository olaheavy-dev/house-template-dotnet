using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;

namespace Api;

/// <summary>
/// The host's limits and browser access, from the Api section (Api__MaxRequestBodyBytes and so on in
/// the environment). Checked when the app starts.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)] // bound from configuration
internal sealed class ApiOptions
{
    public const string Section = "Api";

    /// <summary>
    /// The largest request body read, in bytes; a larger one gets a 413. Files don't come through the
    /// API (browsers upload them straight to storage), so it can be small.
    /// </summary>
    [Range(1, long.MaxValue)]
    public long MaxRequestBodyBytes { get; init; } = 1_048_576;

    /// <summary>How long a request may run before it gets a 504. Streams (SSE) opt out.</summary>
    [Range(1, 3600)]
    public int RequestTimeoutSeconds { get; init; } = 30;

    /// <summary>
    /// Browser origins allowed to call the API (CORS), e.g. https://app.example.com. Empty means
    /// none: the Next.js frontend calls through its own server, so it needs none.
    /// </summary>
    public IReadOnlyList<string> CorsOrigins { get; init; } = [];

    /// <summary>
    /// Requests each caller may make a minute before getting a 429: each signed-in user, otherwise
    /// each client address. Behind the Next.js proxy every anonymous request comes from the
    /// frontend's address, so this is also the ceiling for all anonymous traffic together.
    /// </summary>
    [Range(1, 1_000_000)]
    public int RateLimitPerMinute { get; init; } = 600;

    /// <summary>Whether every CORS origin is a bare http(s) origin: no path, query or wildcard.</summary>
    public bool CorsOriginsAreOrigins() => CorsOrigins.All(origin =>
        Uri.TryCreate(origin, UriKind.Absolute, out var uri)
        && uri.Scheme is "https" or "http"
        && string.Equals(uri.GetLeftPart(UriPartial.Authority), origin, StringComparison.OrdinalIgnoreCase));
}
