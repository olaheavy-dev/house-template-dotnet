using System.Security.Claims;
using Carter;
using Common.Presentation;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Modules.Platform.Presentation;

/// <summary>The signed-in user: their id (the identity provider's subject) and permissions.</summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)] // read by the JSON serializer
public sealed record MeResponse(string Id, IReadOnlyList<string> Permissions);

/// <summary>
/// GET /v1/me: the smallest protected endpoint, the example every new one copies. It needs a
/// signed-in user with <see cref="PlatformPermissions.ReadMe"/>, which Keycloak's default roles
/// give everyone. Replace or extend it once the product has a user module.
/// </summary>
public sealed class MeEndpoint : ICarterModule
{
    public void AddRoutes(IEndpointRouteBuilder app)
    {
        app.MapGet("v1/me", (ClaimsPrincipal user) =>
                TypedResults.Ok(new MeResponse(user.FindFirstValue(SignedInUser.IdClaim)!, user.PermissionsOf())))
            .RequirePermission(PlatformPermissions.ReadMe)
            .WithName("GetMe")
            .WithTags("Me")
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);
    }
}
