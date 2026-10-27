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

    // IntegrationEventsSetEveryProperty: a parameter with the property's name that is never assigned.
    public sealed class RefundIssued
    {
        public RefundIssued(System.Guid refundId, string reason) { RefundId = refundId; }

        public System.Guid RefundId { get; }

        public string? Reason { get; }
    }

    // Not a violation: the property is assigned from a parameter with another name, through a chained constructor.
    public sealed class CustomerMoved
    {
        public CustomerMoved(string newAddress) : this(newAddress, true) { }

        private CustomerMoved(string address, bool confirmed) { Address = address; Confirmed = confirmed; }

        public string Address { get; }

        public bool Confirmed { get; }
    }
}

namespace Common.Domain.Broken
{
    // SharedKernelDependsOnNoModule: the shared kernel depending on a module.
    public sealed class Leak { public Modules.Beta.Domain.Invoice? Invoice; }
}

namespace Modules.Beta.IntegrationEvents
{
    // Not a violation: a property deliberately set to its default value.
    public sealed class ImportStarted
    {
        public ImportStarted(System.Guid importId) { ImportId = importId; IsProcessed = false; Attempts = 0; }

        public System.Guid ImportId { get; }

        public bool IsProcessed { get; }

        public int Attempts { get; }
    }

    // Not a violation: a property over an explicit field the constructor sets.
    public sealed class LabelChanged
    {
        private readonly string _label;

        public LabelChanged(string label) { _label = label; }

        public string Label => _label;
    }
}
