using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RedShirt.Api.Example.Connectors.Foo.Core.Services;
using RedShirt.Api.Example.Connectors.Foo.Implementation.Factories;
using RedShirt.Api.Example.Connectors.Foo.Implementation.Services;
using RedShirt.Api.Example.Connectors.Foo.Implementation.Services.Resilience;
using RedShirt.Example.Api.Common.Extensions;

namespace RedShirt.Api.Example.Connectors.Foo.Implementation.Extensions;

public static class ServiceCollectionExtensions
{
    public const string ConfigurationSectionName = "Connectors:Foo";

    public static IServiceCollection AddFooConnector(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddCommonServices()
            .AddHttpClient()
            .Configure<FooApiClientFactory.ConfigurationModel>(
                configuration.GetSection(ConfigurationSectionName))
            .AddSingleton<IFooExceptionArbiterService, FooExceptionArbiterService>()
            .AddSingleton<IFooRetryWrapperService, FooRetryWrapperService>()
            .AddSingleton<IFooApiClientFactory, FooApiClientFactory>()
            .AddSingleton<IFooConnector, FooConnector>();

        return services;
    }
}
