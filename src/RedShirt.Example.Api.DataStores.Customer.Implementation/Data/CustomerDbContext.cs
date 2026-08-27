using Microsoft.EntityFrameworkCore;
using RedShirt.Example.Api.DataStores.Customer.Implementation.Entities;

namespace RedShirt.Example.Api.DataStores.Customer.Implementation.Data;

/// <summary>
///     Entity Framework Core <see cref="DbContext" /> for the Customer table on MariaDB / MySQL.
///     Schema is owned by the separate Schema repository; this context maps to existing tables.
/// </summary>
internal sealed class CustomerDbContext(DbContextOptions<CustomerDbContext> options) : DbContext(options)
{
    public DbSet<CustomerEntity> Customers => Set<CustomerEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CustomerEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).HasMaxLength(320).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(256).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique();
        });
    }
}