using Moq;
using RedShirt.Example.Api.DataStores.Common.Services;
using RedShirt.Example.Api.DataStores.Customer.Implementation.Data;
using RedShirt.Example.Api.DataStores.Customer.Implementation.Factories;

namespace RedShirt.Example.Api.DataStores.Customer.Implementation.UnitTests.Tests.Factories;

public class CustomerDbContextFactoryTests
{
    [Fact]
    public async Task CreateDbContextAsync_ResolvesNamedConnectionStringAndReturnsContext()
    {
        const string connectionStringName = "primary";
        const string connectionString = "Server=localhost;Database=example;User ID=x;Password=y;";

        var source = new Mock<IConnectionStringSource>(MockBehavior.Strict);
        source
            .Setup(s => s.GetConnectionStringAsync(connectionStringName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(connectionString);

        var factory = new CustomerDbContextFactory(source.Object);

        await using var context = await factory.CreateDbContextAsync(connectionStringName,
            TestContext.Current.CancellationToken);

        Assert.IsType<CustomerDbContext>(context);
        Assert.NotNull(context.Customers);
        source.Verify(
            s => s.GetConnectionStringAsync(connectionStringName, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}