using Common.Presentation;
using ErrorOr;
using Immediate.Handlers.Shared;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Modules.Platform.Application.Flags;

namespace Modules.Platform.Presentation;

/// <summary>GET /v1/flags: the public feature flags as the signed-in user sees them (key, value, variant; never rules).</summary>
internal static class FlagsEndpoint
{
    internal static void MapFlags(this IEndpointRouteBuilder app)
    {
        app.MapGet("v1/flags", async (
                IHandler<ListFlagsQuery, ErrorOr<FlagsResponse>> handler,
                CancellationToken cancellationToken) =>
                ApiResults.Ok(await handler.HandleAsync(new ListFlagsQuery(), cancellationToken)))
            .RequirePermission(PlatformPermissions.ReadFlags)
            .WithName("ListFlags")
            .WithTags("Flags")
            .Produces<FlagsResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
