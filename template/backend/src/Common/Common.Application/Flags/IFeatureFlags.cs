namespace Common.Application.Flags;

/// <summary>
/// Feature flags, evaluated for the signed-in user of the current request (percentage, time-window and
/// targeting rules see who they are). Handlers ask this, never the feature-management library. A flag that
/// is not defined is off.
/// </summary>
public interface IFeatureFlags
{
    ValueTask<bool> IsEnabledAsync(string feature, CancellationToken cancellationToken);

    /// <summary>The name of the variant this user is assigned for <paramref name="feature"/>, or null when it has none.</summary>
    ValueTask<string?> VariantAsync(string feature, CancellationToken cancellationToken);
}
