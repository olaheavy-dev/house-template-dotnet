using Common.Presentation;
using ErrorOr;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Platform.Application.Ping;

namespace Modules.Platform.Presentation;

/// <summary>GET /v1/platform/ping. One static class per feature; the module's `Map…Endpoints` calls it.</summary>
internal static class PingEndpoint
{
    internal static void MapPing(this IEndpointRouteBuilder app)
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
