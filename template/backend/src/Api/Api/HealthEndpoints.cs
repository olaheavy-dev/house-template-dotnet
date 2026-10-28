using System.Text.Json.Serialization;
using Common.Infrastructure;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Api;

/// <summary>What /health/live and /health/ready answer; <c>failing</c> names the checks that failed.</summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)] // read by the JSON serializer
internal sealed record HealthResponse(
    string Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Failing = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Commit = null);

internal static class HealthEndpoints
{
    /// <summary>
    /// /health/live says the process is up and touches no dependency, so a slow database can't get
    /// the container restarted. /health/ready runs every check tagged <see cref="ReadinessChecks.Tag"/>
    /// and answers 503 with the failing names until all pass. The same shape as the FastAPI layer.
    /// </summary>
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var health = app.MapGroup("health").WithTags("Health");

        // The commit the image was built from (the GIT_COMMIT build argument), so what is running can be told from a deploy.
        health.MapGet("live", (IConfiguration configuration) =>
                TypedResults.Ok(new HealthResponse("ok", Commit: configuration["GIT_COMMIT"] is { Length: > 0 } commit ? commit : null)))
            .WithName("GetLiveness");

        health.MapGet("ready", async Task<Results<Ok<HealthResponse>, JsonHttpResult<HealthResponse>>> (
                HealthCheckService checks,
                CancellationToken cancellationToken) =>
            {
                var report = await checks.CheckHealthAsync(
                    check => check.Tags.Contains(ReadinessChecks.Tag), cancellationToken);
                var failing = report.Entries
                    .Where(entry => entry.Value.Status == HealthStatus.Unhealthy)
                    .Select(entry => entry.Key)
                    .Order(StringComparer.Ordinal)
                    .ToList();
                return failing.Count == 0
                    ? TypedResults.Ok(new HealthResponse("ok"))
                    : TypedResults.Json(new HealthResponse("unavailable", failing), statusCode: StatusCodes.Status503ServiceUnavailable);
            })
            .WithName("GetReadiness")
            .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);

        return app;
    }
}
