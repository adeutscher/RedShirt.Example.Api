using Microsoft.Extensions.Options;
using RedShirt.Example.Api.Connectors.Foo.Implementation.Clients;

namespace RedShirt.Example.Api.Connectors.Foo.Implementation.Factories;

internal interface IFooApiClientFactory
{
    IFooApiClient CreateFooApiClient();
}

internal sealed class FooApiClientFactory(
    IHttpClientFactory httpClientFactory,
    IOptions<FooApiClientFactory.ConfigurationModel> configuration) : IFooApiClientFactory
{
    public IFooApiClient CreateFooApiClient()
    {
        var httpClient = httpClientFactory.CreateClient(nameof(FooApiClient));
        return new FooApiClient(httpClient, configuration.Value.BaseUrl);
    }

    internal sealed class ConfigurationModel
    {
        public required string BaseUrl { get; init; }
    }
}