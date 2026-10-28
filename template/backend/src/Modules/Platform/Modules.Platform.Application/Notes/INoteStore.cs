using Common.Application;
using Modules.Platform.Domain;

namespace Modules.Platform.Application.Notes;

/// <summary>Where notes are kept. The application asks for what it needs; Infrastructure answers.</summary>
public interface INoteStore : IUnitOfWork
{
    Task AddAsync(Note note, CancellationToken cancellationToken);

    /// <summary>The owner's notes with an id below <paramref name="before"/> (all of them when null), newest first, at most <paramref name="take"/>.</summary>
    Task<IReadOnlyList<Note>> ListNewestAsync(string ownerId, Guid? before, int take, CancellationToken cancellationToken);
}
