using Amazon.IoT;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Aws.Services;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Services;
using RedShirt.Example.Api.Common.Aws.Extensions;

namespace RedShirt.Example.Api.ClientEvents.Library.Mqtt.Aws.Extensions;

public static class ServiceCollectionExtensions
{
    private const string ConfigurationSectionName =
        $"{Mqtt.Extensions.ServiceCollectionExtensions.ConfigurationSectionName}:AWS";

    public static IServiceCollection AddAwsMqttClientEvents(this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddAwsServiceWithLocalSupport<IAmazonIoT>()
            .Configure<MqttBrokerUrlResolver.ConfigurationModel>(
                configuration.GetSection(ConfigurationSectionName));

        services.TryAddSingleton<IMqttBrokerUrlResolver, MqttBrokerUrlResolver>();

        return services;
    }
}