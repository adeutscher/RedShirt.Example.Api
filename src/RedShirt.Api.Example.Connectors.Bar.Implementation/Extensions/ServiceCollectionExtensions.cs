using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Api.Example.Connectors.Bar.Core.Services;
using RedShirt.Api.Example.Connectors.Bar.Implementation.Clients;
using RedShirt.Api.Example.Connectors.Bar.Implementation.Factories;
using RedShirt.Api.Example.Connectors.Bar.Implementation.Services;
using RedShirt.Api.Example.Connectors.Bar.Implementation.Services.Resilience;
using RedShirt.Example.Api.Common.Extensions;

namespace RedShirt.Api.Example.Connectors.Bar.Implementation.Extensions;

public static class ServiceCollectionExtensions
{
    public const string ConfigurationSectionName = "Connectors:Bar";

    public static IServiceCollection AddBarConnector(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddCommonServices()
            .Configure<BarApiClientFactory.ConfigurationModel>(
                configuration.GetSection(ConfigurationSectionName))
            .Configure<BarApiRequestHandlerRetryWrapperService.ConfigurationModel>(
                configuration.GetSection(ConfigurationSectionName))
            .Configure<BarRetryWrapperService.ConfigurationModel>(
                configuration.GetSection(ConfigurationSectionName))
            .AddTransient<BarApiClientHandler>()
            .AddSingleton<IBarApiRequestHandlerRetryWrapperService, BarApiRequestHandlerRetryWrapperService>()
            .AddSingleton<IBarExceptionArbiterService, BarExceptionArbiterService>()
            .AddSingleton<IBarRetryWrapperService, BarRetryWrapperService>()
            .AddSingleton<IBarApiClientFactory, BarApiClientFactory>()
            .AddSingleton<IBarConnector, BarConnector>();

        services
            .AddHttpClient(nameof(BarApiClient))
            .AddHttpMessageHandler<BarApiClientHandler>();

        return services;
    }
}