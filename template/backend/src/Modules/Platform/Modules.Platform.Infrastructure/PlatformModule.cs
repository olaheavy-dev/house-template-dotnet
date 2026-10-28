using Common.Infrastructure.Messaging;
using Common.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Platform.Application;
using Modules.Platform.Application.Notes;
using Modules.Platform.Infrastructure.Persistence;

namespace Modules.Platform.Infrastructure;

/// <summary>Registers the Platform module: its handlers and what they need.</summary>
public static class PlatformModule
{
    public static IServiceCollection AddPlatformModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var title = configuration["Api:Title"];
        services.AddSingleton(new ServiceName(string.IsNullOrWhiteSpace(title) ? "api" : title));
        services.AddModuleDbContext<PlatformDbContext>(configuration, PlatformDbContext.Schema);
        services.AddScoped<INoteStore, NoteStore>();
        services.AddScoped<IDataSeeder, PlatformSeeder>();
        services.AddIntegrationEventSubscribers(AssemblyReference.Assembly);
        services.AddValidatorsFromAssembly(Application.AssemblyReference.Assembly, includeInternalTypes: true);
        return services.AddModulesPlatformApplicationHandlers();
    }
}
