using Modules.Platform.Domain;

namespace Modules.Platform.Application.Notes;

/// <summary>
/// Reads notes for the cache. Registered as a singleton that opens its own short-lived scope for each lookup,
/// because the cache runs a lookup once for every request waiting on the same key: the request that started it
/// may end first, and nothing tied to that request (its scope, its DbContext) may be used by the lookup.
/// </summary>
public interface INoteReader
{
    Task<Note?> FindAsync(Guid id, CancellationToken cancellationToken);
}
