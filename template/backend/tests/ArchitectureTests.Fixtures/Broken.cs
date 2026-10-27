// Each type below breaks exactly one rule in ArchitectureTests/Rules.cs (named beside it).
#pragma warning disable

namespace Modules.Alpha.Domain
{
    // DomainDependsOnNoOuterLayer: the domain reaching up into its own Application layer.
    public sealed class Order { public Modules.Alpha.Application.OrderService? Service; }
}

namespace Modules.Alpha.Application
{
    public sealed class OrderService
    {
        // ModulesAreIsolated: one module reaching into another module's Domain.
        public Modules.Beta.Domain.Invoice? Invoice;

        // NoMachineClock: the machine clock instead of TimeProvider.
        public System.DateTimeOffset Now() => System.DateTimeOffset.UtcNow;
    }

    // ApplicationDependsOnNoOuterLayer: Application using its Infrastructure.
    public sealed class Saver { public Modules.Alpha.Infrastructure.Repository? Repository; }

    // HandlersAreNotPublic: a public handler other modules could call directly.
    [Immediate.Handlers.Shared.Handler]
    public static partial class PublicHandler
    {
        public sealed record Query;

        private static System.Threading.Tasks.ValueTask<int> HandleAsync(Query query, System.Threading.CancellationToken token) =>
            System.Threading.Tasks.ValueTask.FromResult(1);
    }
}

namespace Modules.Alpha.Presentation
{
    // PresentationDependsOnNoInfrastructure: an endpoint going around its handler.
    public sealed class Endpoint { public Modules.Alpha.Infrastructure.Repository? Repository; }
}

namespace Modules.Alpha.Infrastructure
{
    public sealed class Repository;
}

namespace Modules.Beta.Domain
{
    public sealed class Invoice;
}

namespace Modules.Beta.IntegrationEvents
{
    // IntegrationEventsSetEveryProperty: Email is never set, so it is always null on the wire.
    public sealed class InvoicePaid(System.Guid invoiceId)
    {
        public System.Guid InvoiceId { get; } = invoiceId;

        public string? Email { get; init; }
    }
}

namespace Common.Domain.Broken
{
    // SharedKernelDependsOnNoModule: the shared kernel depending on a module.
    public sealed class Leak { public Modules.Beta.Domain.Invoice? Invoice; }
}
