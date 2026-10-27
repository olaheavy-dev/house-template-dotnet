using Common.Application;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Platform.Application;

namespace Modules.Platform.Infrastructure;

/// <summary>Registers the Platform module: its handlers and what they need.</summary>
public static class PlatformModule
{
    public static IServiceCollection AddPlatformModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var title = configuration["Api:Title"];
        services.AddSingleton(new ServiceName(string.IsNullOrWhiteSpace(title) ? "api" : title));
        return services.AddHandlersFrom(Application.AssemblyReference.Assembly);
    }
}
