using System.Globalization;
using System.Threading.RateLimiting;
using Common.Infrastructure;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http.Timeouts;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Options;
using ServiceDefaults;

namespace Api;

/// <summary>
/// The host's limits: request body size, request timeouts, a rate limit per caller, CORS for the
/// configured origins only, and security headers. Allowed hosts are ASP.NET Core's own AllowedHosts
/// setting, and graceful shutdown its default 30 seconds.
/// </summary>
internal static class Hardening
{
    /// <summary>The security-header policy for the development API reference, whose page runs scripts.</summary>
    public const string DocsPolicy = "docs";

    /// <summary>The code of the 429 problem.</summary>
    public const string RateLimitedCode = "request.rate_limited";

    public static IServiceCollection AddHardening(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatedOptions<ApiOptions>(configuration.GetSection(ApiOptions.Section))
            .Validate(options => options.CorsOriginsAreOrigins(),
                "Api:CorsOrigins must be bare origins such as https://app.example.com: no path, trailing slash or wildcard.");

        services.AddOptions<KestrelServerOptions>().Configure<IOptions<ApiOptions>>((kestrel, api) =>
        {
            kestrel.Limits.MaxRequestBodySize = api.Value.MaxRequestBodyBytes;
            // Kestrel writes "Server: Kestrel" itself, after any middleware could remove it.
            kestrel.AddServerHeader = false;
        });

        services.AddRequestTimeouts();
        services.AddOptions<RequestTimeoutOptions>().Configure<IOptions<ApiOptions>>((timeouts, api) =>
            timeouts.DefaultPolicy = new RequestTimeoutPolicy
            {
                Timeout = TimeSpan.FromSeconds(api.Value.RequestTimeoutSeconds),
                TimeoutStatusCode = StatusCodes.Status504GatewayTimeout,
            });

        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>().Configure<IOptions<ApiOptions>>((limiter, api) =>
        {
            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                context.Request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
                    ? RateLimitPartition.GetNoLimiter("health")
                    : RateLimitPartition.GetFixedWindowLimiter(Caller(context), _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = api.Value.RateLimitPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));
            limiter.OnRejected = RefuseAsync;
        });

        services.AddCors();
        services.AddOptions<CorsOptions>().Configure<IOptions<ApiOptions>>((cors, api) =>
            cors.AddDefaultPolicy(policy => policy
                .WithOrigins([.. api.Value.CorsOrigins])
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                .WithExposedHeaders(ServiceDefaultsExtensions.RequestIdHeader, "Retry-After")));

        services.AddSecurityHeaderPolicies()
            .SetDefaultPolicy(policy => policy
                .AddDefaultApiSecurityHeaders()
                // Responses stay out of shared caches; an endpoint that wants caching gets its own policy.
                .AddCustomHeader("Cache-Control", "no-store"))
            .AddPolicy(DocsPolicy, policy => policy.AddDefaultSecurityHeaders());

        return services;
    }

    /// <summary>
    /// CORS, sign-in, the rate limit, permissions and timeouts, in that order: after the error
    /// handlers, so a refusal is a problem too; the rate limit after sign-in, so a signed-in user is
    /// limited as themselves, and before permissions, so refused requests count too.
    /// </summary>
    public static WebApplication UseHardening(this WebApplication app)
    {
        app.UseCors();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.UseRequestTimeouts();
        return app;
    }

    // The signed-in user when there is one; otherwise the client's address.
    private static string Caller(HttpContext context) =>
        context.User.Identity is { IsAuthenticated: true, Name: { } name }
            ? "user:" + name
            : "address:" + context.Connection.RemoteIpAddress;

    private static async ValueTask RefuseAsync(OnRejectedContext rejected, CancellationToken cancellationToken)
    {
        var context = rejected.HttpContext;
        if (rejected.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
        }

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails =
            {
                Status = StatusCodes.Status429TooManyRequests,
                Detail = "Too many requests: try again later.",
                Extensions = { ["code"] = RateLimitedCode },
            },
        });
    }
}
