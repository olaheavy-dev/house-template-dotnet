using Common.Application.Flags;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.FeatureManagement;

namespace Common.Infrastructure.Flags;

public static class FlagServices
{
    /// <summary>
    /// Feature flags from configuration (<c>feature_management:feature_flags</c>, Microsoft's schema; environment
    /// variables such as <c>feature_management__feature_flags__0__enabled</c> in production, appsettings in development)
    /// with the percentage, time-window and targeting filters, targeting the signed-in user, and variants. Evaluations are recorded on the request's trace for flags with
    /// <c>telemetry.enabled</c>. Handlers use <see cref="IFeatureFlags"/>; endpoints use <c>.WithFeatureGate(...)</c>.
    /// </summary>
    public static IServiceCollection AddFeatureFlags(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddHttpContextAccessor();
        // Reads the feature_management section of the app's configuration (Microsoft's feature-flag schema).
        services.AddScopedFeatureManagement(configuration)
            .WithTargeting<HttpTargetingContextAccessor>();
        services.AddScoped<IFeatureFlags, FeatureFlags>();
        return services;
    }
}
