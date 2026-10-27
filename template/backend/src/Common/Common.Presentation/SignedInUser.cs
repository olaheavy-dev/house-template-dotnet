using System.Security.Claims;
using Microsoft.AspNetCore.Builder;

namespace Common.Presentation;

/// <summary>
/// The signed-in user: their id, and what they may do. A permission is a string such as
/// <c>me.read</c>, carried as <see cref="PermissionClaim"/> claims: from Keycloak, the API client's
/// roles in the token; in development, the X-Dev-User stand-in. Endpoints require one with
/// <see cref="RequirePermission{TBuilder}"/>.
/// </summary>
public static class SignedInUser
{
    /// <summary>The user's stable id: the identity provider's subject, never an email.</summary>
    public const string IdClaim = "sub";

    public const string PermissionClaim = "permission";

    /// <summary>Every permission. Only the development stand-in user holds it; never read from a token.</summary>
    public const string AllPermissions = "*";

    /// <summary>Signed-in users holding <paramref name="permission"/> only: 401 for anyone else, 403 without it.</summary>
    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(policy => policy.RequireClaim(PermissionClaim, permission, AllPermissions));

    /// <summary>The permissions <paramref name="user"/> holds, in order.</summary>
    public static IReadOnlyList<string> PermissionsOf(this ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return [.. user.FindAll(PermissionClaim).Select(claim => claim.Value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
    }
}
