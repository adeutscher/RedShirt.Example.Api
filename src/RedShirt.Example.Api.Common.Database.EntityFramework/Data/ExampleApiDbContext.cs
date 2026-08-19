using Microsoft.EntityFrameworkCore;
using RedShirt.Example.Api.Common.Database.EntityFramework.Models;

namespace RedShirt.Example.Api.Common.Database.EntityFramework.Data;

/// <summary>
///     Entity Framework Core <see cref="DbContext" /> for MariaDB / MySQL.
///     Schema is owned by the separate Schema repository; this context maps to existing tables.
/// </summary>
public sealed class ExampleApiDbContext(DbContextOptions<ExampleApiDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customer");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Email).HasMaxLength(320).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(256).IsRequired();
            entity.HasIndex(e => e.Email).IsUnique();
        });
    }
}
