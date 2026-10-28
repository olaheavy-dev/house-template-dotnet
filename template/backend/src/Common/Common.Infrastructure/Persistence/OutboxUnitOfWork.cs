using Common.Application;
using Microsoft.EntityFrameworkCore;

namespace Common.Infrastructure.Persistence;

/// <summary>
/// The base of a module's store: <see cref="Db"/> is the module's context, and
/// <see cref="SaveChangesAsync"/> commits its changes together with the events published through
/// <see cref="PublishAsync{TEvent}"/>.
/// </summary>
public abstract class OutboxUnitOfWork<TContext>(ModuleOutbox<TContext> outbox) : IUnitOfWork
    where TContext : DbContext
{
    protected TContext Db => outbox.Db;

    public async ValueTask PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : class
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);
        await outbox.PublishAsync(integrationEvent);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        outbox.SaveChangesAndFlushAsync(cancellationToken);
}
