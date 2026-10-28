using Microsoft.EntityFrameworkCore;
using Wolverine.EntityFrameworkCore;

namespace Common.Infrastructure.Persistence;

/// <summary>
/// A module's context together with the transactional outbox over it. Registered for every module
/// context by <see cref="PersistenceServices.AddModuleDbContext{TContext}"/>; it keeps the messaging
/// library's types out of the modules.
/// </summary>
public sealed class ModuleOutbox<TContext>(IDbContextOutbox<TContext> outbox)
    where TContext : DbContext
{
    public TContext Db => outbox.DbContext;

    public async ValueTask PublishAsync<TEvent>(TEvent integrationEvent)
        where TEvent : class =>
        await outbox.PublishAsync(integrationEvent);

    public Task SaveChangesAndFlushAsync(CancellationToken cancellationToken) =>
        outbox.SaveChangesAndFlushMessagesAsync(cancellationToken);
}
