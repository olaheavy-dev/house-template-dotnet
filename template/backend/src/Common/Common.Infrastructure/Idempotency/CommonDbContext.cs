using Microsoft.EntityFrameworkCore;

namespace Common.Infrastructure.Idempotency;

/// <summary>The shared kernel's own tables, in the <see cref="Schema"/> schema: the remembered idempotency keys.</summary>
public sealed class CommonDbContext(DbContextOptions<CommonDbContext> options) : DbContext(options)
{
    public const string Schema = "common";
    public const string Table = "idempotency_keys";

    public DbSet<IdempotencyRecord> IdempotencyKeys => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<IdempotencyRecord>(record =>
        {
            record.ToTable(Table);
            record.HasKey(entry => new { entry.Scope, entry.Key });
            record.Property(entry => entry.Scope).HasMaxLength(200);
            record.Property(entry => entry.Key).HasMaxLength(255);
            record.Property(entry => entry.Fingerprint).HasMaxLength(64).IsRequired();
            record.Property(entry => entry.State).HasMaxLength(16).IsRequired();
            record.Property(entry => entry.ContentType).HasMaxLength(200);
            record.Property(entry => entry.Location).HasMaxLength(2000);
            record.HasIndex(entry => entry.ExpiresAt);
        });
    }
}
