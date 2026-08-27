using Microsoft.EntityFrameworkCore;
using RedShirt.Example.Api.DataStores.Customer.Implementation.Data;
using RedShirt.Example.Api.DataStores.Customer.Implementation.Entities;

namespace RedShirt.Example.Api.DataStores.Customer.Implementation.UnitTests.Tests.Data;

public class CustomerDbContextTests
{
    private const string DummyConnectionString = "Server=localhost;Database=example;User ID=x;Password=y;";

    private static CustomerDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CustomerDbContext>()
            .UseMySQL(DummyConnectionString)
            .Options;
        return new CustomerDbContext(options);
    }

    [Fact]
    public void Customers_DbSet_IsAvailable()
    {
        using var context = CreateContext();

        Assert.NotNull(context.Customers);
    }

    [Fact]
    public void Model_MapsCustomerEntityToCustomersTableWithExpectedShape()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(CustomerEntity));

        Assert.NotNull(entityType);
        Assert.Equal("Customers", entityType.GetTableName());
        Assert.Equal(nameof(CustomerEntity.Id), entityType.FindPrimaryKey()?.Properties.Single().Name);
        Assert.Equal(320, entityType.FindProperty(nameof(CustomerEntity.Email))?.GetMaxLength());
        Assert.Equal(256, entityType.FindProperty(nameof(CustomerEntity.DisplayName))?.GetMaxLength());
        Assert.Contains(entityType.GetIndexes(),
            index => index.IsUnique && index.Properties.Single().Name == nameof(CustomerEntity.Email));
    }
}