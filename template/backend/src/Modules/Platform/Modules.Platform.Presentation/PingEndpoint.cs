using Carter;
using Common.Presentation;
using ErrorOr;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Platform.Application.Ping;

namespace Modules.Platform.Presentation;

/// <summary>GET /v1/platform/ping. One Carter module per feature; Carter finds them at start-up.</summary>
public sealed class PingEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("v1/platform/ping", async (
                IHandler<GetPingQuery, ErrorOr<PingResponse>> handler,
                CancellationToken cancellationToken) =>
                ApiResults.Ok(await handler.HandleAsync(new GetPingQuery(), cancellationToken)))
            .WithName("GetPing")
            .WithTags("Platform")
            .Produces<PingResponse>();
    }
}
