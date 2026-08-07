using Amazon.SimpleSystemsManagement;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Common.Aws.Extensions;
using RedShirt.Example.Api.Common.Aws.SsmSecretManager.Services;
using RedShirt.Example.Api.Common.Aws.SsmSecretManager.Services.Resilience;
using RedShirt.Example.Api.Common.SecretManagers.Core.Services;

namespace RedShirt.Example.Api.Common.Aws.SsmSecretManager.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Add common services and abstractions for pulling information from a secret manager.
    /// </summary>
    public static IServiceCollection AddSecretManagerSsm(this IServiceCollection services,
        IConfigurationRoot configuration)
    {
        return services
            .AddAwsResiliency()
            .AddAwsServiceWithLocalSupport<IAmazonSimpleSystemsManagement>()
            .AddSingleton<ISsmExceptionArbiterService, SsmExceptionArbiterService>()
            .AddSingleton<ISsmRetryWrapperService, SsmRetryWrapperService>()
            .AddSingleton<ISecretManagerService, SsmSecretManagerService>();
    }
}