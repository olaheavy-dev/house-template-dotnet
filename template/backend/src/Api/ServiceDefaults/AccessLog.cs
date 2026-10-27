using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Routing;

namespace ServiceDefaults;

/// <summary>
/// Logs the matched route's template (<c>/v1/users/{id}</c>) in place of the path, so ids, emails
/// and tokens in URLs never reach the logs; the query string never does either, and a path no route
/// matched is logged as <c>Unknown</c>. Health probes aren't logged. Microsoft's
/// AddHttpLoggingRedaction does the same but is still experimental (EXTEXP0013), so this uses the
/// stable interceptor instead.
/// </summary>
internal sealed class AccessLog : IHttpLoggingInterceptor
{
    public ValueTask OnRequestAsync(HttpLoggingInterceptorContext logContext)
    {
        ArgumentNullException.ThrowIfNull(logContext);
        if (logContext.HttpContext.Request.Path.StartsWithSegments(ServiceDefaultsExtensions.HealthPaths, StringComparison.OrdinalIgnoreCase))
        {
            logContext.LoggingFields = HttpLoggingFields.None;
        }
        else
        {
            // The route is known only once the endpoint has run; it's added with the response.
            logContext.TryDisable(HttpLoggingFields.RequestPath);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask OnResponseAsync(HttpLoggingInterceptorContext logContext)
    {
        ArgumentNullException.ThrowIfNull(logContext);
        if (logContext.IsAnyEnabled(HttpLoggingFields.Response))
        {
            var route = (logContext.HttpContext.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText;
            logContext.AddParameter("Route", route is null ? "Unknown" : "/" + route.TrimStart('/'));
        }

        return ValueTask.CompletedTask;
    }
}
