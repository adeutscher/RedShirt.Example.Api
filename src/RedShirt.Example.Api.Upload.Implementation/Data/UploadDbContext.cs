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
            entity.Property(e => e.State).IsRequired();
            entity.Property(e => e.FileName).HasMaxLength(512).IsRequired();
            entity.Property(e => e.Sha256Checksum)
                .HasMaxLength(64)
                .IsFixedLength()
                .HasColumnType("char(64)")
                .IsUnicode(false);
            entity.Property(e => e.Flags).HasConversion<int>().IsRequired();
            entity.Property(e => e.IdempotencyKey).HasMaxLength(128).IsRequired();
            entity.HasIndex(e => e.IdempotencyKey).IsUnique();
            entity.HasIndex(e => e.Sha256Checksum);
        });

        modelBuilder.Entity<UploadEventEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EventType).IsRequired();
            entity.HasIndex(e => e.UploadId);
        });
    }
}