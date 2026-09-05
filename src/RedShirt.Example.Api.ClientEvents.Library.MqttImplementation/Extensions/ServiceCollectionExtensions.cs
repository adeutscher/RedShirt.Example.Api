using Amazon.IoT;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RedShirt.Example.Api.ClientEvents.Library.Core.Services;
using RedShirt.Example.Api.ClientEvents.Library.MqttImplementation.Factories;
using RedShirt.Example.Api.ClientEvents.Library.MqttImplementation.Services;
using RedShirt.Example.Api.ClientEvents.Library.MqttImplementation.Services.Resilience;
using RedShirt.Example.Api.Common.Aws.Extensions;
using RedShirt.Example.Api.Common.Extensions;

namespace RedShirt.Example.Api.ClientEvents.Library.MqttImplementation.Extensions;

public static class ServiceCollectionExtensions
{
    public const string ConfigurationSectionName = "ClientEvents:Mqtt";

    public static IServiceCollection AddMqttClientEvents(this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddCommonServices()
            .AddAwsServiceWithLocalSupport<IAmazonIoT>()
            .Configure<ApiMqttClientFactory.ConfigurationModel>(
                configuration.GetSection(ConfigurationSectionName))
            .Configure<MqttClientEventsRetryWrapperService.ConfigurationModel>(
                configuration.GetSection(ConfigurationSectionName));

        services.TryAddSingleton<IMqttClientEventsExceptionArbiterService, MqttClientEventsExceptionArbiterService>();
        services.TryAddSingleton<IMqttClientEventsRetryWrapperService, MqttClientEventsRetryWrapperService>();
        services.TryAddSingleton<IMqttClientFactory, ApiMqttClientFactory>();
        services.TryAddSingleton(typeof(IApiClientEventSender<>), typeof(MqttApiClientEventSender<>));
        services.TryAddSingleton(typeof(IApiClientEventReceiver<>), typeof(MqttApiClientEventReceiver<>));

        return services;
    }
}