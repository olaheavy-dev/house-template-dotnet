using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
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
