using Microsoft.Extensions.Options;
using RedShirt.Example.Api.Connectors.Bar.Implementation.Clients;
using RedShirt.Example.Api.Connectors.Bar.Implementation.Services.Resilience;
using RedShirt.Example.Api.Connectors.Common.Http.Models;

namespace RedShirt.Example.Api.Connectors.Bar.Implementation.Factories;

internal interface IBarApiClientFactory
{
    /// <summary>
    ///     Creates a client for one attempt. Callers dispose it, which closes its connection when
    ///     <see cref="BarApiClientFactory.ConfigurationModel.ClientPerRequest" /> is set and leaves the pool open
    ///     otherwise.
    /// </summary>
    IBarApiClient CreateBarApiClient();
}

internal sealed class BarApiClientFactory(
    IHttpClientFactory httpClientFactory,
    IBarApiRequestHandlerRetryWrapperService apiRequestRetryWrapperService,
    IOptions<BarApiClientFactory.ConfigurationModel> configuration) : IBarApiClientFactory
{
    public IBarApiClient CreateBarApiClient()
    {
        var httpClient = configuration.Value.ClientPerRequest
            ? new HttpClient(new BarApiClientHandler(apiRequestRetryWrapperService)
            {
                InnerHandler = new HttpClientHandler()
            })
            : httpClientFactory.CreateClient(nameof(BarApiClient));
        return new BarApiClient(httpClient, configuration.Value.BaseUrl);
    }

    internal sealed class ConfigurationModel : IConnectorConnectionConfiguration
    {
        public required string BaseUrl { get; init; }

        public required bool ClientPerRequest { get; init; }

        public required int? HandlerLifetimeSeconds { get; init; }

        public required int? MaxConnectionsPerServer { get; init; }
    }
}