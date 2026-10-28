namespace Common.Application;

/// <summary>
/// A module's changes and the integration events they cause, committed together or not at all:
/// the events are stored with the data (a transactional outbox) and delivered afterwards, so no
/// event is lost when the process stops between the two. Modules never see the messaging library.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Queues <paramref name="integrationEvent"/> (a type from a module's IntegrationEvents project) for delivery on commit.</summary>
    ValueTask PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : class;

    /// <summary>Saves the module's changes and stores the queued events in one transaction, then delivers them.</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
