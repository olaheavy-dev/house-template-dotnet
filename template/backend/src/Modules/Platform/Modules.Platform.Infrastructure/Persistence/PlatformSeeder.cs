using Common.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Modules.Platform.Domain;

namespace Modules.Platform.Infrastructure.Persistence;

/// <summary>A note for the development stand-in user (X-Dev-User: dev), so a fresh database isn't empty.</summary>
internal sealed class PlatformSeeder(PlatformDbContext db, TimeProvider clock) : IDataSeeder
{
    public const string Owner = "dev";

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (await db.Notes.AnyAsync(note => note.OwnerId == Owner, cancellationToken))
        {
            return;
        }

        db.Notes.Add(Note.Create(Guid.CreateVersion7(clock.GetUtcNow()), Owner, "Welcome: this note was added by make seed.").Value);
        await db.SaveChangesAsync(cancellationToken);
    }
}
