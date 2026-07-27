using Amazon.SimpleSystemsManagement;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Common.Aws.Extensions;
using RedShirt.Example.Api.Common.Aws.SsmSecretManager.Services;
using RedShirt.Example.Api.Common.SecretManagers.Core.Services;

namespace RedShirt.Example.Api.Common.Aws.SsmSecretManager.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Add common services and abstractions for pulling information from a secret manager.
    /// </summary>
    /// <param name="services"></param>
    /// <returns></returns>
    public static IServiceCollection AddSecretManagerSsm(this IServiceCollection services)
    {
        return services
            .AddAwsServiceWithLocalSupport<IAmazonSimpleSystemsManagement>()
            .AddSingleton<ISecretManagerService, SsmSecretManagerService>();
    }
}