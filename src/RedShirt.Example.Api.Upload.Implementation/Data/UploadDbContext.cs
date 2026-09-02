using Microsoft.EntityFrameworkCore;
using RedShirt.Example.Api.Upload.Implementation.Entities;

namespace RedShirt.Example.Api.Upload.Implementation.Data;

internal sealed class UploadDbContext(DbContextOptions<UploadDbContext> options) : DbContext(options)
{
    public DbSet<UploadAggregateEntity> UploadAggregates => Set<UploadAggregateEntity>();
    public DbSet<UploadEventEntity> UploadEvents => Set<UploadEventEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UploadAggregateEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UploadedByUserId).HasMaxLength(256).IsRequired();
            entity.Property(e => e.State).HasMaxLength(32).IsRequired();
            entity.Property(e => e.FileName).HasMaxLength(512).IsRequired();
            entity.Property(e => e.IdempotencyKey).HasMaxLength(128).IsRequired();
            entity.HasIndex(e => e.IdempotencyKey).IsUnique();
        });

        modelBuilder.Entity<UploadEventEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => e.UploadId);
        });
    }
}