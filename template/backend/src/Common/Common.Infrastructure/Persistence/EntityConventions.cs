using Common.Domain;
using Microsoft.EntityFrameworkCore;

namespace Common.Infrastructure.Persistence;

public static class EntityConventions
{
    /// <summary>
    /// Maps every <see cref="Entity"/> in the model the same way: <see cref="Entity.Version"/> is
    /// Postgres' xmin system column, a concurrency token (a stale save throws DbUpdateConcurrencyException).
    /// Call it last in OnModelCreating.
    /// </summary>
    public static ModelBuilder ApplyEntityConventions(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        foreach (var entity in modelBuilder.Model.GetEntityTypes().Where(type => typeof(Entity).IsAssignableFrom(type.ClrType)))
        {
            // Npgsql maps a uint row version to the xmin system column, so no column is created.
            modelBuilder.Entity(entity.ClrType).Property(nameof(Entity.Version)).IsRowVersion();
        }

        return modelBuilder;
    }
}
