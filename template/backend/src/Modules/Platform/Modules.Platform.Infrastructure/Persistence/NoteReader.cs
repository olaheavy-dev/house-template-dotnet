using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modules.Platform.Application.Notes;
using Modules.Platform.Domain;

namespace Modules.Platform.Infrastructure.Persistence;

/// <summary>Looks a note up in a scope of its own, so it does not depend on the request that asked.</summary>
internal sealed class NoteReader(IServiceScopeFactory scopes) : INoteReader
{
    public async Task<Note?> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        return await db.Notes.AsNoTracking().FirstOrDefaultAsync(note => note.Id == id, cancellationToken);
    }
}
