using Microsoft.Extensions.Options;
using RedShirt.Api.Example.Connectors.Foo.Implementation.Clients;
using RedShirt.Example.Api.Common.SecretManagers.Core.Services;

namespace RedShirt.Api.Example.Connectors.Foo.Implementation.Factories;

internal interface IFooApiClientFactory
{
    Task<IFooApiClient> CreateFooApiClientAsync(CancellationToken cancellationToken = default);
}

internal sealed class FooApiClientFactory(
    ISecretManagerCacheService secretManager,
    IHttpClientFactory httpClientFactory,
    IOptions<FooApiClientFactory.ConfigurationModel> configuration) : IFooApiClientFactory
{
    public async Task<IFooApiClient> CreateFooApiClientAsync(CancellationToken cancellationToken = default)
    {
        var httpClient = httpClientFactory.CreateClient(nameof(FooApiClient));

        var apiKey = await secretManager.GetSecretAsync(configuration.Value.ApiKeyPath,
            cancellationToken: cancellationToken);
        httpClient.DefaultRequestHeaders.Remove("x-api-key");
        httpClient.DefaultRequestHeaders.Add("x-api-key", apiKey);

        return new FooApiClient(httpClient, configuration.Value.BaseUrl);
    }

    internal sealed class ConfigurationModel
    {
        public required string BaseUrl { get; init; }

        /// <summary>
        ///     Secret-manager path for the Foo API key (same pattern as connection-string paths).
        /// </summary>
        public required string ApiKeyPath { get; init; }
    }
}
