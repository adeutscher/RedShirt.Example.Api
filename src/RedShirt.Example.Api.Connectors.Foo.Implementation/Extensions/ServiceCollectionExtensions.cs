using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Example.Api.Common.Extensions;
using RedShirt.Example.Api.Connectors.Foo.Core.Services;
using RedShirt.Example.Api.Connectors.Foo.Implementation.Clients;
using RedShirt.Example.Api.Connectors.Foo.Implementation.Factories;
using RedShirt.Example.Api.Connectors.Foo.Implementation.Services;
using RedShirt.Example.Api.Connectors.Foo.Implementation.Services.Resilience;

namespace RedShirt.Example.Api.Connectors.Foo.Implementation.Extensions;

public static class ServiceCollectionExtensions
{
    public const string ConfigurationSectionName = "Connectors:Foo";

    public static IServiceCollection AddFooConnector(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddCommonServices()
            .Configure<FooApiClientFactory.ConfigurationModel>(
                configuration.GetSection(ConfigurationSectionName))
            .Configure<FooApiRequestHandlerRetryWrapperService.ConfigurationModel>(
                configuration.GetSection(ConfigurationSectionName))
            .Configure<FooRetryWrapperService.ConfigurationModel>(
                configuration.GetSection(ConfigurationSectionName))
            .AddTransient<FooApiClientHandler>()
            .AddSingleton<IFooApiRequestHandlerRetryWrapperService, FooApiRequestHandlerRetryWrapperService>()
            .AddSingleton<IFooExceptionArbiterService, FooExceptionArbiterService>()
            .AddSingleton<IFooRetryWrapperService, FooRetryWrapperService>()
            .AddSingleton<IFooApiClientFactory, FooApiClientFactory>()
            .AddSingleton<IFooConnector, FooConnector>();

        services
            .AddHttpClient(nameof(FooApiClient))
            .AddHttpMessageHandler<FooApiClientHandler>();

        return services;
    }
}