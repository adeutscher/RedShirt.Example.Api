using RedShirt.Example.Api.Common.Aws.SsmSecretManager.Extensions;
using RedShirt.Example.Api.Common.RateLimiting.Extensions;
using RedShirt.Example.Api.Common.Redis.Extensions;
using RedShirt.Example.Api.Common.SecretManagers.Core.Extensions;
using RedShirt.Example.Api.ExceptionHandlers;
using RedShirt.Example.Api.Implementations.ExampleItem.Extensions;

namespace RedShirt.Example.Api.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection ConfigureApiServices(this IServiceCollection serviceCollection,
        IConfiguration configuration)
    {
        return serviceCollection
            .AddLogging()
            .AddProblemDetails()
            .AddExceptionHandler<ApiExceptionHandler>()
            // Rate Limiting
            .ConsiderAddingRateLimitingPolicies(configuration)
            // Common
            .AddSecretManagerCore(configuration)
            .AddSecretManagerSsm() // AWS binding
            .AddRedisImplementations(configuration)
            // App-specific
            .ConfigureApiImplementations(configuration);
    }
}