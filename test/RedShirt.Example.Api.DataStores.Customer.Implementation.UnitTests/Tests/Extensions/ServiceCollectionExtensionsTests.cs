using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using RedShirt.Example.Api.Common.Database.Services;
using RedShirt.Example.Api.Common.Distributed.Services.Abstractions;
using RedShirt.Example.Api.DataStores.Customer.Core.Services;
using RedShirt.Example.Api.DataStores.Customer.Implementation.Extensions;
using RedShirt.Example.Api.DataStores.Customer.Implementation.Factories;
using RedShirt.Example.Api.DataStores.Customer.Implementation.Repositories;
using RedShirt.Example.Api.DataStores.Customer.Implementation.Services;

namespace RedShirt.Example.Api.DataStores.Customer.Implementation.UnitTests.Tests.Extensions;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCustomers_RegistersCustomerServicesAsSingletons()
    {
        var configuration = new ConfigurationBuilder().Build();
        var connectionStringSource = new Mock<IConnectionStringSource>(MockBehavior.Strict);
        var cacheService = new Mock<IRemoteCacheService>(MockBehavior.Strict);
        var services = new ServiceCollection()
            .AddCustomers(configuration)
            .AddSingleton(connectionStringSource.Object)
            .AddSingleton(cacheService.Object);

        using var provider = services.BuildServiceProvider();

        var factory1 = provider.GetRequiredService<ICustomerDbContextFactory>();
        var factory2 = provider.GetRequiredService<ICustomerDbContextFactory>();
        var repository1 = provider.GetRequiredService<ICustomerRepository>();
        var repository2 = provider.GetRequiredService<ICustomerRepository>();
        var service1 = provider.GetRequiredService<ICustomerService>();
        var service2 = provider.GetRequiredService<ICustomerService>();

        Assert.IsType<CustomerDbContextFactory>(factory1);
        Assert.Same(factory1, factory2);
        Assert.IsType<CustomerRepository>(repository1);
        Assert.Same(repository1, repository2);
        Assert.IsType<CustomerService>(service1);
        Assert.Same(service1, service2);
    }

    [Fact]
    public void AddCustomers_ReturnsSameServiceCollection()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        var returned = services.AddCustomers(configuration);

        Assert.Same(services, returned);
    }
}