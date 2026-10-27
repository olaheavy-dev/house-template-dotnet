using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace ServiceDefaults;

public static class ServiceDefaultsExtensions
{
    // Probes hit these every few seconds; their spans and access lines would drown the real traffic.
    internal const string HealthPaths = "/health";

    /// <summary>
    /// Telemetry, the access log and resilient HTTP clients. Logs go to the console as JSON outside
    /// development (Logging:Console:FormatterName in appsettings.json).
    /// </summary>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddTelemetry();
        builder.Services.AddAccessLog();
        // ASP.NET Core's hosting diagnostics put the raw request path (ids, emails) in a scope on
        // every log line, and log each full URL with its query string. Off: the access log and the
        // RequestId and TraceId scopes cover the same ground without them.
        builder.Logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.None);
        // Every HttpClient from the factory gets retries with backoff, a circuit breaker and timeouts.
        builder.Services.ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler());
        return builder;
    }

    /// <summary>Request ids, then the access log, outermost, so both cover every response.</summary>
    public static WebApplication UseServiceDefaults(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);
        app.UseMiddleware<RequestIds>();
        app.UseHttpLogging();
        return app;
    }

    /// <summary>
    /// Traces, metrics and logs through OpenTelemetry. Nothing leaves the process unless
    /// OTEL_EXPORTER_OTLP_ENDPOINT is set (the Aspire dashboard locally, LangSmith or any OTLP
    /// collector in production). The service is named by OTEL_SERVICE_NAME, else
    /// OpenTelemetry:ServiceName (the project's slug, from appsettings.json), else the app's name;
    /// the SDK alone would call it unknown_service:dotnet, the same as every other app.
    /// </summary>
    private static void AddTelemetry(this IHostApplicationBuilder builder)
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        var serviceName = new[]
            {
                builder.Configuration["OTEL_SERVICE_NAME"],
                builder.Configuration["OpenTelemetry:ServiceName"],
            }.FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? builder.Environment.ApplicationName;

        var telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation())
            .WithTracing(tracing => tracing
                .AddSource(builder.Environment.ApplicationName)
                .AddAspNetCoreInstrumentation(aspNetCore => aspNetCore.Filter =
                    context => !context.Request.Path.StartsWithSegments(HealthPaths, StringComparison.OrdinalIgnoreCase))
                .AddHttpClientInstrumentation());

        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            telemetry.UseOtlpExporter();
        }
    }

    /// <summary>One line per request: method, route, status and duration (<see cref="AccessLog"/>).</summary>
    private static void AddAccessLog(this IServiceCollection services)
    {
        services.AddHttpLogging(logging =>
        {
            logging.LoggingFields = HttpLoggingFields.RequestMethod | HttpLoggingFields.RequestPath
                | HttpLoggingFields.ResponseStatusCode | HttpLoggingFields.Duration;
            logging.CombineLogs = true;
        });
        services.AddHttpLoggingInterceptor<AccessLog>();
    }
}
