using Carter;
using Common.Presentation;
using ErrorOr;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Sample.Application.Status;

namespace Modules.Sample.Presentation;

/// <summary>GET /v1/sample-route/status.</summary>
public sealed class StatusEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("v1/sample-route/status", async (
                IHandler<GetSampleStatusQuery, ErrorOr<SampleStatus>> handler,
                CancellationToken cancellationToken) =>
                ApiResults.Ok(await handler.HandleAsync(new GetSampleStatusQuery(), cancellationToken)))
            .WithName("GetSampleStatus")
            .WithTags("Sample")
            .Produces<SampleStatus>();
    }
}
