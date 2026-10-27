using Common.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Common.Infrastructure;

public static class CommonServices
{
    /// <summary>What every module relies on. Modules then add themselves (Add&lt;Module&gt;Module).</summary>
    public static IServiceCollection AddCommon(this IServiceCollection services) =>
        services.AddCommonApplication();
}
