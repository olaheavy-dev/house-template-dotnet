using Modules.Platform.Domain;

namespace Modules.Platform.Application.Notes;

/// <summary>Where notes are kept. The application asks for what it needs; Infrastructure answers.</summary>
public interface INoteStore
{
    Task AddAsync(Note note, CancellationToken cancellationToken);

    Task<Note?> FindAsync(Guid id, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
