using Microsoft.Extensions.DependencyInjection;
using Moq;
using RedShirt.Example.Api.Common.Database.EntityFramework.Extensions;
using RedShirt.Example.Api.Common.Database.EntityFramework.Factories;
using RedShirt.Example.Api.Common.Database.Services;

namespace RedShirt.Example.Api.Common.Database.EntityFramework.UnitTests.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddEntityFramework_RegistersFactoryAsSingleton()
    {
        var connectionStringSource = new Mock<IConnectionStringSource>(MockBehavior.Strict);
        var services = new ServiceCollection()
            .AddEntityFramework()
            .AddSingleton(connectionStringSource.Object);

        using var provider = services.BuildServiceProvider();

        var factory1 = provider.GetRequiredService<IExampleApiDbContextFactory>();
        var factory2 = provider.GetRequiredService<IExampleApiDbContextFactory>();

        Assert.IsType<ExampleApiDbContextFactory>(factory1);
        Assert.Same(factory1, factory2);
    }

    [Fact]
    public void AddEntityFramework_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();

        var returned = services.AddEntityFramework();

        Assert.Same(services, returned);
    }
}
