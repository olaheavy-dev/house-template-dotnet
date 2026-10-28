using Common.Application.Flags;
using Microsoft.FeatureManagement;

namespace Common.Infrastructure.Flags;

/// <summary>Microsoft.FeatureManagement behind <see cref="IFeatureFlags"/>: one snapshot per request, so a flag reads the same throughout it.</summary>
internal sealed class FeatureFlags(IVariantFeatureManagerSnapshot manager) : IFeatureFlags
{
    public async ValueTask<bool> IsEnabledAsync(string feature, CancellationToken cancellationToken) =>
        await manager.IsEnabledAsync(feature, cancellationToken);

    public async ValueTask<string?> VariantAsync(string feature, CancellationToken cancellationToken) =>
        (await manager.GetVariantAsync(feature, cancellationToken))?.Name;
}
