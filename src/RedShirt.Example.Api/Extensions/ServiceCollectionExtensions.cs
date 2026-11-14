using RedShirt.Example.Api.BackgroundJobs.Implementation.Extensions;
using RedShirt.Example.Api.BackgroundJobs.Logic.Extensions;
using RedShirt.Example.Api.BackgroundJobs.Runner.Extensions;
using RedShirt.Example.Api.BackgroundJobs.Submission.Services;
using RedShirt.Example.Api.ClientEvents.Domains.Example.Extensions;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Aws.Extensions;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Extensions;
using RedShirt.Example.Api.Common.Aws.SsmSecretManager.Extensions;
using RedShirt.Example.Api.Common.Distributed.Extensions;
using RedShirt.Example.Api.Common.Extensions;
using RedShirt.Example.Api.Common.RateLimiting.Extensions;
using RedShirt.Example.Api.Common.SecretManagers.Core.Extensions;
using RedShirt.Example.Api.Connectors.Bar.Implementation.Extensions;
using RedShirt.Example.Api.Connectors.Foo.Implementation.Extensions;
using RedShirt.Example.Api.Core.Extensions;
using RedShirt.Example.Api.DataStores.Common.DapperMySql.Extensions;
using RedShirt.Example.Api.DataStores.Customer.Implementation.Extensions;
using RedShirt.Example.Api.DataStores.ExampleItem.Extensions;
using RedShirt.Example.Api.DataStores.Order.Extensions;
using RedShirt.Example.Api.DataStores.Product.Implementation.Extensions;
using RedShirt.Example.Api.ExceptionHandlers;
using RedShirt.Example.Api.Upload.Implementation.Extensions;

// scaffold:data-store-usings

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
            // Host
            ////
            .ConsiderAddingAuthentication(configuration)
            .ConsiderAddingStubAuthorizationScopedResourcePolicies(configuration)
            // Common
            ////
            .AddCommonServices()
            .AddSecretManagerCore(configuration)
            // Secret Manager
            ////
            // If you wish to swap out SSM for Azure Key Vault as your secret manager provider, adjust the below code
            //.AddSecretManagerAzureKeyVault(configuration)
            //.AddSecretManagerDocker(configuration)
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
            .AddCustomers(configuration) // Customer store via Entity Framework Core (MariaDB / MySQL).
            .AddOrders()
            .AddProducts(configuration)
            .AddUploads(configuration)
            // scaffold:data-store-registrations
            // Background jobs
            ////
            // Jobs are submitted to the BackgroundJob table. To hand them to an applied JobWorker instead, replace
            // AddBackgroundJobTable's IBackgroundJobSubmitter with one that publishes to the worker's queue.
            .AddBackgroundJobTable(configuration)
            // Job logic, as an applied JobWorker's Core.Logic project would hold it.
            .AddBackgroundJobLogic()
            // The in-API runner. Switch it off with BACKGROUND_JOBS__RUNNER_ENABLED=false.
            .AddBackgroundJobRunner(configuration)
            .AddMqttClientEvents(configuration)
            .AddAwsMqttClientEvents(configuration)
            .AddExampleClientEventsDomain();
    }
}