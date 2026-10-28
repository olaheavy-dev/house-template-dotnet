using System.Text.Json;
using Common.Presentation.Idempotency;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

/// <summary>
/// Endpoints only the tests have: a start-up filter maps them after the app's own pipeline, so
/// registering it (<see cref="AddTo"/>) puts them behind the app's real middleware.
/// </summary>
internal sealed class TestEndpoints : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        next(app);
        app.UseEndpoints(endpoints =>
        {
            // Reads the whole body, as any endpoint that binds one does.
            endpoints.MapPost("test/echo", (JsonElement body) => TypedResults.Ok(body));
            // Idempotency-Key probes: how often each ran, a request that waits to be released, one that fails once.
            endpoints.MapPost("test/idem-required", () => TypedResults.Ok(new { runs = Interlocked.Increment(ref IdempotencyProbe.Runs) }))
                .WithIdempotencyKey(required: true);
            endpoints.MapPost("test/idem-slow", async (CancellationToken cancellationToken) =>
                {
                    var runs = Interlocked.Increment(ref IdempotencyProbe.Runs);
                    IdempotencyProbe.Started.Release();
                    await IdempotencyProbe.Gate.WaitAsync(cancellationToken);
                    return TypedResults.Ok(new { runs });
                })
                .WithIdempotencyKey();
            endpoints.MapPost("test/idem-fails-once", () =>
                    Interlocked.Increment(ref IdempotencyProbe.Runs) == 1
                        ? Results.Problem(statusCode: 500)
                        : Results.Ok(new { runs = IdempotencyProbe.Runs }))
                .WithIdempotencyKey();
            // Runs until the request is cancelled.
            endpoints.MapGet("test/slow", async (CancellationToken cancellationToken) =>
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
                return TypedResults.Ok();
            });
        });
    };

    public static void AddTo(IServiceCollection services) => services.AddSingleton<IStartupFilter, TestEndpoints>();
}

/// <summary>What the idempotency test endpoints share. Tests that use it run one at a time.</summary>
internal static class IdempotencyProbe
{
#pragma warning disable CA2211, S2223 // a counter the endpoints and the tests share
    public static int Runs;
#pragma warning restore CA2211, S2223

    public static SemaphoreSlim Started { get; } = new(0);

    public static SemaphoreSlim Gate { get; } = new(0);

    public static void Reset()
    {
        Runs = 0;
        while (Started.Wait(0))
        {
        }

        while (Gate.Wait(0))
        {
        }
    }
}
