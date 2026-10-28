using Common.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Modules.Platform.Application.Notes;
using Modules.Platform.Domain;

namespace Modules.Platform.Infrastructure.Persistence;

internal sealed class NoteStore(ModuleOutbox<PlatformDbContext> outbox) : OutboxUnitOfWork<PlatformDbContext>(outbox), INoteStore
{
    public async Task AddAsync(Note note, CancellationToken cancellationToken) =>
        await Db.Notes.AddAsync(note, cancellationToken);

    public Task<Note?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        Db.Notes.FirstOrDefaultAsync(note => note.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Note>> ListNewestAsync(string ownerId, Guid? before, int take, CancellationToken cancellationToken) =>
        await Db.Notes
            .Where(note => note.OwnerId == ownerId && (before == null || note.Id.CompareTo(before.Value) < 0))
            .OrderByDescending(note => note.Id)
            .Take(take)
            .ToListAsync(cancellationToken);
}
