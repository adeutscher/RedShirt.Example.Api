using Microsoft.EntityFrameworkCore;
using RedShirt.Example.Api.Common.Database.EntityFramework.Data;
using RedShirt.Example.Api.Common.Database.EntityFramework.Models;

namespace RedShirt.Example.Api.Common.Database.EntityFramework.UnitTests.Tests.Data;

public class ExampleApiDbContextTests
{
    private const string DummyConnectionString = "Server=localhost;Database=example;User ID=x;Password=y;";

    private static ExampleApiDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ExampleApiDbContext>()
            .UseMySQL(DummyConnectionString)
            .Options;
        return new ExampleApiDbContext(options);
    }

    [Fact]
    public void Model_MapsCustomerToCustomerTableWithExpectedShape()
    {
        using var context = CreateContext();
        var entityType = context.Model.FindEntityType(typeof(Customer));

        Assert.NotNull(entityType);
        Assert.Equal("Customer", entityType.GetTableName());
        Assert.Equal(nameof(Customer.Id), entityType.FindPrimaryKey()?.Properties.Single().Name);
        Assert.Equal(320, entityType.FindProperty(nameof(Customer.Email))?.GetMaxLength());
        Assert.Equal(256, entityType.FindProperty(nameof(Customer.DisplayName))?.GetMaxLength());
        Assert.Contains(entityType.GetIndexes(),
            index => index.IsUnique && index.Properties.Single().Name == nameof(Customer.Email));
    }

    [Fact]
    public void Customers_DbSet_IsAvailable()
    {
        using var context = CreateContext();

        Assert.NotNull(context.Customers);
    }
}
