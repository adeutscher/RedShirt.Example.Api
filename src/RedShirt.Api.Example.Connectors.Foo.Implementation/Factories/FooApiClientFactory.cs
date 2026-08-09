using Microsoft.Extensions.Options;
using RedShirt.Api.Example.Connectors.Foo.Implementation.Clients;

namespace RedShirt.Api.Example.Connectors.Foo.Implementation.Factories;

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

        // ReSharper disable once InvertIf
        if (!string.IsNullOrWhiteSpace(configuration.Value.ApiKey))
        {
            httpClient.DefaultRequestHeaders.Remove("x-api-key");
            httpClient.DefaultRequestHeaders.Add("x-api-key", configuration.Value.ApiKey);
        }

        return new FooApiClient(httpClient, configuration.Value.BaseUrl);
    }

    public sealed class ConfigurationModel
    {
        public required string BaseUrl { get; init; }
        public string? ApiKey { get; init; }
    }
}
