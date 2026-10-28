using Microsoft.EntityFrameworkCore;
using Modules.Platform.Application.Notes;
using Modules.Platform.Domain;

namespace Modules.Platform.Infrastructure.Persistence;

internal sealed class NoteStore(PlatformDbContext db) : INoteStore
{
    public async Task AddAsync(Note note, CancellationToken cancellationToken) =>
        await db.Notes.AddAsync(note, cancellationToken);

    public Task<Note?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        db.Notes.FirstOrDefaultAsync(note => note.Id == id, cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
