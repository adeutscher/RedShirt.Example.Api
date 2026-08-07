using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Common.Azure.Extensions;
using RedShirt.Example.Api.Common.Azure.KeyVaultSecretManager.Factories;
using RedShirt.Example.Api.Common.Azure.KeyVaultSecretManager.Services;
using RedShirt.Example.Api.Common.SecretManagers.Core.Services;

namespace RedShirt.Example.Api.Common.Azure.KeyVaultSecretManager.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSecretManagerAzureKeyVault(this IServiceCollection services,
        IConfigurationRoot configuration)
    {
        return services
            // Ensure that common Azure services are wired up
            .AddCommonAzureServices()
            // Azure Key Vault
            .Configure<AzureKeyVaultClientFactory.ConfigurationModel>(
                configuration.GetSection("Common:Secrets:AzureKeyVault"))
            .AddSingleton<IAzureKeyVaultClientFactory, AzureKeyVaultClientFactory>()
            .AddSingleton<IAzureKeyVaultClientSource, AzureKeyVaultClientSource>()
            .AddSingleton<ISecretManagerService, AzureKeyVaultService>();
    }
}