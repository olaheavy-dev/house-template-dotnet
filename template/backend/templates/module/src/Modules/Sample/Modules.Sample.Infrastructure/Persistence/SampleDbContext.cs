using Common.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Modules.Sample.Domain;

namespace Modules.Sample.Infrastructure.Persistence;

/// <summary>The Sample module's tables, in the <see cref="Schema"/> schema.</summary>
public sealed class SampleDbContext(DbContextOptions<SampleDbContext> options) : DbContext(options)
{
    public const string Schema = "sample-route";

    public DbSet<SampleItem> Items => Set<SampleItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<SampleItem>(item =>
        {
            item.ToTable("items");
            item.HasKey(entry => entry.Id);
            item.Property(entry => entry.Name).HasMaxLength(SampleItem.MaxLength).IsRequired();
        });
        modelBuilder.ApplyEntityConventions();
    }
}
