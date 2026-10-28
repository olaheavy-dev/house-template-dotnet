using JetBrains.Annotations;

namespace Common.Domain;

/// <summary>
/// What every stored thing has: an id, when it was created and last changed, and a version that
/// stops two writers overwriting each other. Persistence sets the last three; the domain only reads them.
/// </summary>
public abstract class Entity
{
    public Guid Id { get; protected init; }

    [UsedImplicitly] // set by the audit interceptor
    public DateTimeOffset CreatedAt { get; private set; }

    [UsedImplicitly] // set by the audit interceptor
    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Postgres' row version (xmin): a save fails if the row changed since it was read.</summary>
    [UsedImplicitly] // set by EF Core
    public uint Version { get; private set; }
}
