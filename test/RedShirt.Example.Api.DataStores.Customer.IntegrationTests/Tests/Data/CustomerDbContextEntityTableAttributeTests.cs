using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using RedShirt.Example.Api.DataStores.Customer.Implementation.Data;

namespace RedShirt.Example.Api.DataStores.Customer.IntegrationTests.Tests.Data;

public class CustomerDbContextEntityTableAttributeTests
{
    [Fact]
    public void EveryDbContextEntity_HasTableAttributeWithName()
    {
        var entityTypes = typeof(CustomerDbContext)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.PropertyType.IsGenericType
                               && property.PropertyType.GetGenericTypeDefinition() == typeof(DbSet<>))
            .Select(property => property.PropertyType.GetGenericArguments()[0])
            .ToArray();

        // Sanity-check our search for types
        Assert.NotEmpty(entityTypes);

        foreach (var entityType in entityTypes)
        {
            var tableAttribute = entityType.GetCustomAttribute<TableAttribute>();
            Assert.NotNull(tableAttribute);
            Assert.False(string.IsNullOrWhiteSpace(tableAttribute.Name),
                $"{entityType.Name} must specify a non-empty table name via [Table].");
        }
    }
}
