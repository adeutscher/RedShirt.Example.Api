using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Common.Database.Extensions;
using RedShirt.Example.Api.DataStores.Customer.Core.Repositories;
using RedShirt.Example.Api.DataStores.Customer.Core.Services;
using RedShirt.Example.Api.DataStores.Customer.Implementation.Factories;
using RedShirt.Example.Api.DataStores.Customer.Implementation.Repositories;

namespace RedShirt.Example.Api.DataStores.Customer.Implementation.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCustomers(this IServiceCollection services, IConfigurationRoot configuration)
    {
        return services
            .AddCommonDatabase()
            .AddSingleton<ICustomerDbContextFactory, CustomerDbContextFactory>()
            .AddSingleton<ICustomerRepository, CustomerRepository>()
            .AddSingleton<ICustomerService, CustomerService>();
    }
}