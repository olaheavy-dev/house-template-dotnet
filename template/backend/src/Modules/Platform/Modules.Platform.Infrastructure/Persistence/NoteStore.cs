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
}
