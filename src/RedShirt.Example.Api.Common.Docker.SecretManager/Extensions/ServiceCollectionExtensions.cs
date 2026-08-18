using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Common.Docker.SecretManager.Services;
using RedShirt.Example.Api.Common.SecretManagers.Core.Services;

namespace RedShirt.Example.Api.Common.Docker.SecretManager.Extensions;

public static class ServiceCollectionExtensions
{
    public const string ConfigurationSectionName = "Common:Secrets:Docker";

    public static IServiceCollection AddSecretManagerDocker(this IServiceCollection services,
        IConfiguration configuration)
    {
        return services
            .Configure<DockerSecretManagerService.ConfigurationModel>(
                configuration.GetSection(ConfigurationSectionName))
            .AddSingleton<ISecretManagerService, DockerSecretManagerService>();
    }
}