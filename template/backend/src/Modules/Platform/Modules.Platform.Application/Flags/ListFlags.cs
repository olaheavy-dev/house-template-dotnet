using Common.Application.Flags;
using ErrorOr;
using Immediate.Handlers.Shared;
using Microsoft.Extensions.Options;

namespace Modules.Platform.Application.Flags;

public sealed record ListFlagsQuery;

/// <summary>One flag as the signed-in user sees it: whether it is on for them and the variant they were given. Never the rules.</summary>
public sealed record FlagResponse(string Key, bool Enabled, string? Variant);

public sealed record FlagsResponse(IReadOnlyList<FlagResponse> Flags);

/// <summary>The public flags, evaluated for the signed-in user. A front end reads this instead of knowing the rules.</summary>
[Handler]
internal sealed partial class ListFlags(IFeatureFlags flags, IOptions<PublicFlagsOptions> options)
{
    private async ValueTask<ErrorOr<FlagsResponse>> HandleAsync(ListFlagsQuery query, CancellationToken cancellationToken)
    {
        var result = new List<FlagResponse>();
        foreach (var key in options.Value.Public.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            result.Add(new FlagResponse(key, await flags.IsEnabledAsync(key, cancellationToken), await flags.VariantAsync(key, cancellationToken)));
        }

        return new FlagsResponse(result);
    }
}
