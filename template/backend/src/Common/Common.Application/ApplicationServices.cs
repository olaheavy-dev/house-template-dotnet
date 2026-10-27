using Microsoft.Extensions.DependencyInjection;

namespace Common.Application;

public static class ApplicationServices
{
    /// <summary>The clock every module shares. Handlers come from each module's generated Add…Handlers().</summary>
    public static IServiceCollection AddCommonApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
