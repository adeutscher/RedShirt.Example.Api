using RedShirt.Example.Api.Common.Aws.SsmSecretManager.Extensions;
using RedShirt.Example.Api.Common.Distributed.Extensions;
using RedShirt.Example.Api.Common.Extensions;
using RedShirt.Example.Api.Common.RateLimiting.Extensions;
using RedShirt.Example.Api.Common.SecretManagers.Core.Extensions;
using RedShirt.Example.Api.ExceptionHandlers;
using RedShirt.Example.Api.Implementations.ExampleItem.Extensions;

namespace RedShirt.Example.Api.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection ConfigureApiServices(this IServiceCollection serviceCollection,
        IConfigurationRoot configuration)
    {
        return serviceCollection
            .AddLogging()
            .AddProblemDetails()
            .AddExceptionHandler<ApiExceptionHandler>()
            // Common
            .AddCommonServices()
            .AddSecretManagerCore(configuration)
            .AddSecretManagerSsm(configuration)
            .AddDistributedServices(configuration)
            // Rate Limiting
            .ConsiderAddingRateLimitingPolicies(configuration)
            // App-specific
            .ConfigureApiImplementations(configuration);
    }
}
