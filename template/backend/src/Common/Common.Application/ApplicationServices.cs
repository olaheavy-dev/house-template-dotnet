using System.Reflection;
using Common.Application.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Common.Application;

public static class ApplicationServices
{
    /// <summary>The dispatcher and the clock every module shares.</summary>
    public static IServiceCollection AddCommonApplication(this IServiceCollection services)
    {
        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }

    /// <summary>Registers every command and query handler a module's assembly declares.</summary>
    public static IServiceCollection AddHandlersFrom(this IServiceCollection services, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        Type[] handlerInterfaces =
        [
            typeof(ICommandHandler<>),
            typeof(ICommandHandler<,>),
            typeof(IQueryHandler<,>),
        ];
        var handlers = assembly.GetTypes().Where(type => type is { IsClass: true, IsAbstract: false });
        foreach (var handler in handlers)
        {
            foreach (var contract in handler.GetInterfaces()
                .Where(i => i.IsGenericType && handlerInterfaces.Contains(i.GetGenericTypeDefinition())))
            {
                services.AddScoped(contract, handler);
            }
        }

        return services;
    }
}
