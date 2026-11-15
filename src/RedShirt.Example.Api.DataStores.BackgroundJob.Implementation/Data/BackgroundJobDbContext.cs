using Microsoft.EntityFrameworkCore;
using RedShirt.Example.Api.DataStores.BackgroundJob.Implementation.Entities;

namespace RedShirt.Example.Api.DataStores.BackgroundJob.Implementation.Data;

internal sealed class BackgroundJobDbContext(DbContextOptions<BackgroundJobDbContext> options) : DbContext(options)
{
    public DbSet<BackgroundJobEntity> BackgroundJobs => Set<BackgroundJobEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BackgroundJobEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.State).IsRequired();
            entity.Property(e => e.ParametersJson).IsRequired();
            entity.Property(e => e.LeaseOwner).HasMaxLength(BackgroundJobLimits.LeaseOwnerMaxLength);
            entity.HasIndex(e => new {e.State, e.DueAtUtc});
            entity.HasIndex(e => e.Type);
        });
    }
}

internal static class BackgroundJobLimits
{
    public const int LeaseOwnerMaxLength = 128;
    public const int DetailMaxLength = 4000;
}