using Microsoft.Extensions.Options;
using RedShirt.Example.Api.Connectors.Common.Http.Models;
using RedShirt.Example.Api.Connectors.Foo.Implementation.Clients;
using RedShirt.Example.Api.Connectors.Foo.Implementation.Services.Resilience;

namespace RedShirt.Example.Api.Connectors.Foo.Implementation.Factories;

internal interface IFooApiClientFactory
{
    /// <summary>
    ///     Creates a client for one attempt. Callers dispose it, which closes its connection when
    ///     <see cref="FooApiClientFactory.ConfigurationModel.ClientPerRequest" /> is set and leaves the pool open
    ///     otherwise.
    /// </summary>
    IFooApiClient CreateFooApiClient();
}

internal sealed class FooApiClientFactory(
    IHttpClientFactory httpClientFactory,
    IFooApiRequestHandlerRetryWrapperService apiRequestRetryWrapperService,
    IOptions<FooApiClientFactory.ConfigurationModel> configuration) : IFooApiClientFactory
{
    public IFooApiClient CreateFooApiClient()
    {
        var httpClient = configuration.Value.ClientPerRequest
            ? new HttpClient(new FooApiClientHandler(apiRequestRetryWrapperService)
            {
                InnerHandler = new HttpClientHandler()
            })
            : httpClientFactory.CreateClient(nameof(FooApiClient));
        return new FooApiClient(httpClient, configuration.Value.BaseUrl);
    }

    internal sealed class ConfigurationModel : IConnectorConnectionConfiguration
    {
        public required string BaseUrl { get; init; }

        public required bool ClientPerRequest { get; init; }

        public required int? HandlerLifetimeSeconds { get; init; }

        public required int? MaxConnectionsPerServer { get; init; }
    }
}