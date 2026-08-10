using Microsoft.Extensions.Options;
using RedShirt.Api.Example.Connectors.Foo.Implementation.Clients;

namespace RedShirt.Api.Example.Connectors.Foo.Implementation.Factories;

internal interface IFooApiClientFactory
{
    Task<IFooApiClient> CreateFooApiClientAsync(CancellationToken cancellationToken = default);
}

internal sealed class FooApiClientFactory(
    IHttpClientFactory httpClientFactory,
    IOptions<FooApiClientFactory.ConfigurationModel> configuration) : IFooApiClientFactory
{
    public const string HttpClientName = nameof(FooApiClient);

    public Task<IFooApiClient> CreateFooApiClientAsync(CancellationToken cancellationToken = default)
    {
        var httpClient = httpClientFactory.CreateClient(HttpClientName);
        return Task.FromResult<IFooApiClient>(new FooApiClient(httpClient, configuration.Value.BaseUrl));
    }

    internal sealed class ConfigurationModel
    {
        public required string BaseUrl { get; init; }
    }
}