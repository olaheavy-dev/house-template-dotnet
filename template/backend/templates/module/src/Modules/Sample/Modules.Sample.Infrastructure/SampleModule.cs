using Common.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Sample.Application;
using Modules.Sample.Infrastructure.Persistence;

namespace Modules.Sample.Infrastructure;

/// <summary>Registers the Sample module: its handlers, validators and what they need.</summary>
public static class SampleModule
{
    public static IServiceCollection AddSampleModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddModuleDbContext<SampleDbContext>(configuration, SampleDbContext.Schema);
        services.AddValidatorsFromAssembly(Application.AssemblyReference.Assembly, includeInternalTypes: true);
        return services.AddModulesSampleApplicationHandlers();
    }
}
