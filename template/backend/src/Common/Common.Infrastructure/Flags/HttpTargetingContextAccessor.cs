using Common.Application;
using Microsoft.AspNetCore.Http;
using Microsoft.FeatureManagement.FeatureFilters;

namespace Common.Infrastructure.Flags;

/// <summary>
/// Who a targeting rule sees: the signed-in user's id, and their permissions as the groups, so a flag can be
/// aimed at a person or at everyone holding a permission. Nobody signed in (or no request) is nobody targeted.
/// </summary>
internal sealed class HttpTargetingContextAccessor(IHttpContextAccessor http) : ITargetingContextAccessor
{
    public ValueTask<TargetingContext> GetContextAsync()
    {
        var user = http.HttpContext?.User;
        var id = user?.FindFirst(UserClaims.Id)?.Value;
        return ValueTask.FromResult(new TargetingContext
        {
            UserId = id,
            Groups = id is null ? [] : [.. user!.FindAll(UserClaims.Permission).Select(claim => claim.Value).Distinct(StringComparer.Ordinal)],
        });
    }
}
