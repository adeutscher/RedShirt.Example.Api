using Moq;
using RedShirt.Example.Api.Common.Database.EntityFramework.Data;
using RedShirt.Example.Api.Common.Database.EntityFramework.Factories;
using RedShirt.Example.Api.Common.Database.Services;

namespace RedShirt.Example.Api.Common.Database.EntityFramework.UnitTests.Tests.Factories;

public class ExampleApiDbContextFactoryTests
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

        var factory = new ExampleApiDbContextFactory(source.Object);

        await using var context = await factory.CreateDbContextAsync(connectionStringName,
            TestContext.Current.CancellationToken);

        Assert.IsType<ExampleApiDbContext>(context);
        Assert.NotNull(context.Customers);
        source.Verify(
            s => s.GetConnectionStringAsync(connectionStringName, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
