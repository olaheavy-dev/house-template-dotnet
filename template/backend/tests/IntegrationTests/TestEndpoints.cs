using System.Text.Json;
using Carter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace IntegrationTests;

/// <summary>
/// Endpoints only the tests have: Carter maps every ICarterModule in the container, so registering
/// this one (<see cref="AddTo"/>) puts them behind the app's real middleware.
/// </summary>
internal sealed class TestEndpoints : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        // Reads the whole body, as any endpoint that binds one does.
        app.MapPost("test/echo", (JsonElement body) => TypedResults.Ok(body));
        // Runs until the request is cancelled.
        app.MapGet("test/slow", async (CancellationToken cancellationToken) =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return TypedResults.Ok();
        });
    }

    public static void AddTo(IServiceCollection services) => services.AddSingleton<ICarterModule, TestEndpoints>();
}
