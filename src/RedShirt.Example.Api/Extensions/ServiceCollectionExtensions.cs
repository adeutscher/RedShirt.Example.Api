using RedShirt.Api.Example.Connectors.Bar.Implementation.Extensions;
using RedShirt.Api.Example.Connectors.Foo.Implementation.Extensions;
using RedShirt.Example.Api.Common.Aws.SsmSecretManager.Extensions;
using RedShirt.Example.Api.Common.Database.DapperMySql.Extensions;
using RedShirt.Example.Api.Common.Distributed.Extensions;
using RedShirt.Example.Api.Common.Extensions;
using RedShirt.Example.Api.Common.RateLimiting.Extensions;
using RedShirt.Example.Api.Common.SecretManagers.Core.Extensions;
using RedShirt.Example.Api.Core.Extensions;
using RedShirt.Example.Api.DataStores.ExampleItem.Extensions;
using RedShirt.Example.Api.DataStores.Order.Extensions;
using RedShirt.Example.Api.DataStores.Product.Implementation.Extensions;
using RedShirt.Example.Api.ExceptionHandlers;

namespace RedShirt.Example.Api.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection ConfigureApiServices(this IServiceCollection serviceCollection,
        IConfigurationRoot configuration)
    {
        return serviceCollection
            .AddLogging()
            .AddSingleton(configuration)
            .AddProblemDetails()
            .AddExceptionHandler<ApiExceptionHandler>()
            // Common
            ////
            .AddCommonServices()
            .AddSecretManagerCore(configuration)
            // Secret Manager
            ////
            // If you wish to swap out SSM for Azure Key Vault as your secret manager provider, adjust the below code
            //.AddSecretManagerAzureKeyVault(configuration)
            .AddSecretManagerSsm(configuration)
            // Add distributed services (read: Redis). Note that Redis requires a secret manager for the connection string
            ////
            .AddDistributedServices(configuration)
            // Rate Limiting
            ////
            .ConsiderAddingRateLimitingPolicies(configuration)
            // App-specific
            ////
            .ConfigureApiCore(configuration)
            // Connectors
            .AddFooConnector(configuration)
            .AddBarConnector(configuration)
            // Data Stores
            .AddExampleItem(configuration)
            .AddDapperMySql(configuration) // Add Dapper support for MySQL-based database servers. 
            .AddOrders()
            .AddProducts(configuration);
    }
}