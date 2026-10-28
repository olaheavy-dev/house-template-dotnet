using Common.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Modules.Platform.Domain;

namespace Modules.Platform.Infrastructure.Persistence;

/// <summary>The Platform module's tables, in the <see cref="Schema"/> schema.</summary>
public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options)
{
    public const string Schema = "platform";

    public DbSet<Note> Notes => Set<Note>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Note>(note =>
        {
            note.ToTable("notes");
            note.HasKey(entry => entry.Id);
            note.Property(entry => entry.OwnerId).HasMaxLength(200).IsRequired();
            note.Property(entry => entry.Text).HasMaxLength(Note.MaxLength).IsRequired();
            note.HasIndex(entry => entry.OwnerId);
        });
        modelBuilder.ApplyEntityConventions();
    }
}
