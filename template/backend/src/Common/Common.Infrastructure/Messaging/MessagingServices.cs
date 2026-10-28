using System.Reflection;
using Common.Infrastructure.Persistence;
using JasperFx;
using JasperFx.CodeGeneration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;

namespace Common.Infrastructure.Messaging;

public static class MessagingServices
{
    /// <summary>True while the build runs the app to write the OpenAPI document (the dotnet-getdocument tool's host).</summary>
    private static bool IsWritingTheOpenApiDocument =>
        Assembly.GetEntryAssembly()?.GetName().Name == "GetDocument.Insider";

    /// <summary>The schema of the outbox, inbox and dead-letter tables every module shares.</summary>
    public const string Schema = "wolverine";

    /// <summary>
    /// Wolverine as the durable layer behind integration events: a transactional outbox, an idempotent
    /// inbox and durable local queues on the app's Postgres, no broker. Modules never touch it: they
    /// publish through <see cref="Common.Application.IUnitOfWork"/> and subscribe with
    /// <see cref="AddIntegrationEventSubscribers"/>.
    /// Handler glue code is written ahead of time (<c>make codegen</c>) and compiled into the host; only
    /// in Debug builds, which carry the compiler, may it be generated on the fly.
    /// <c>Messaging:GeneratedCodePath</c> redirects where <c>codegen write</c> puts it (the freshness check uses this).
    /// </summary>
    public static WebApplicationBuilder AddMessaging(this WebApplicationBuilder builder, Assembly hostAssembly)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(hostAssembly);
        var connectionString = builder.Configuration.GetConnectionString(PersistenceServices.ConnectionName);
        var generatedCodePath = builder.Configuration["Messaging:GeneratedCodePath"];
        var isDevelopment = builder.Environment.IsDevelopment();
        builder.Host.UseWolverine(options =>
        {
            // Without a database there is nothing to make durable. The build starts the app to write the OpenAPI
            // document, and must not need (or reach for) a database.
            if (!string.IsNullOrWhiteSpace(connectionString) && !IsWritingTheOpenApiDocument)
            {
                options.PersistMessagesWithPostgresql(connectionString, Schema);
            }

            // The tables are created and updated by the migrate step (Api.dll migrate), not by whatever starts first:
            // in production the API's database user needs no rights to change the schema.
            options.AutoBuildMessageStorageOnStartup = isDevelopment ? AutoCreate.CreateOrUpdate : AutoCreate.None;
            options.UseEntityFrameworkCoreTransactions();
            options.Policies.UseDurableLocalQueues();
            // The generated code is compiled into the host, which is where Wolverine looks for it.
            options.ApplicationAssembly = hostAssembly;
#if DEBUG
            // Debug builds carry the compiler (WolverineFx.RuntimeCompilation): use the generated code when it is
            // there, generate on the fly when it is not, so a new handler works before `make codegen`.
            options.CodeGeneration.TypeLoadMode = TypeLoadMode.Auto;
#else
            // Release builds carry no compiler: the generated code must be there and current.
            options.CodeGeneration.TypeLoadMode = TypeLoadMode.Static;
#endif
            if (!string.IsNullOrWhiteSpace(generatedCodePath))
            {
                options.CodeGeneration.GeneratedCodeOutputPath = generatedCodePath;
            }
        });
        return builder;
    }

    /// <summary>
    /// Registers a module's subscribers: the public classes in <paramref name="assembly"/> with a
    /// <c>Handle(TEvent, ...)</c> method (named *Consumer or *Handler) receive the integration events
    /// they take. Events from other modules arrive only if that module published them.
    /// </summary>
    public static IServiceCollection AddIntegrationEventSubscribers(this IServiceCollection services, Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return services.ConfigureWolverine(options => options.Discovery.IncludeAssembly(assembly));
    }
}
