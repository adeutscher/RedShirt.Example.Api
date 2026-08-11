using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Common.Extensions;
using RedShirt.Example.Api.Connectors.Bar.Core.Services;
using RedShirt.Example.Api.Connectors.Bar.Implementation.Clients;
using RedShirt.Example.Api.Connectors.Bar.Implementation.Factories;
using RedShirt.Example.Api.Connectors.Bar.Implementation.Services;
using RedShirt.Example.Api.Connectors.Bar.Implementation.Services.Resilience;
using RedShirt.Example.Api.Connectors.Common.Http.Services;

namespace RedShirt.Example.Api.Connectors.Bar.Implementation.Extensions;

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
            .Configure<OAuthTokenSource.ConfigurationModel>(
                configuration.GetSection(ConfigurationSectionName))
            .AddSingleton<IOAuthTokenSource, OAuthTokenSource>()
            .AddSingleton<IOAuthTokenCache, OAuthTokenCache>()
            .AddTransient<BarApiClientHandler>()
            .AddSingleton<IBarApiRequestHandlerRetryWrapperService, BarApiRequestHandlerRetryWrapperService>()
            .AddSingleton<IBarExceptionArbiterService, BarExceptionArbiterService>()
            .AddSingleton<IBarRetryWrapperService, BarRetryWrapperService>()
            .AddSingleton<IBarApiClientFactory, BarApiClientFactory>()
            .AddSingleton<IBarConnector, BarConnector>();

        services
            .AddHttpClient(nameof(OAuthTokenSource));

        services
            .AddHttpClient(nameof(BarApiClient))
            .AddHttpMessageHandler<BarApiClientHandler>();

        return services;
    }
}