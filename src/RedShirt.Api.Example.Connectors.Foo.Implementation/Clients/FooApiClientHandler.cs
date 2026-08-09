using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using RedShirt.Example.Api.Common.SecretManagers.Core.Services;
using System.Net;

namespace RedShirt.Api.Example.Connectors.Foo.Implementation.Clients;

/// <summary>
///     Attaches the Foo static API key (resolved from the secret manager) to outbound requests.
///     On <see cref="HttpStatusCode.Unauthorized" />, force-refreshes the key once and retries.
/// </summary>
internal sealed class FooApiClientHandler(
    ISecretManagerCacheService secretManager,
    ILogger<FooApiClientHandler> logger,
    IOptions<FooApiClientHandler.ConfigurationModel> options) : DelegatingHandler
{
    private string? _apiKey;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_apiKey is null)
        {
            _apiKey = await secretManager.GetSecretAsync(options.Value.ApiKeyPath,
                force: false,
                cancellationToken: cancellationToken);
        }

        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 1,
                ShouldHandle = args => args.Outcome.Exception is UnauthorizedException
                    ? PredicateResult.True()
                    : PredicateResult.False(),
                DelayGenerator = static _ => new ValueTask<TimeSpan?>(TimeSpan.Zero),
                OnRetry = async args =>
                {
                    logger.LogDebug("Refreshing Foo API key from secret path {ApiKeyPath}",
                        options.Value.ApiKeyPath);
                    _apiKey = await secretManager.GetSecretAsync(options.Value.ApiKeyPath,
                        force: true,
                        cancellationToken: args.Context.CancellationToken);
                }
            })
            .Build();

        return await pipeline.ExecuteAsync(async token =>
        {
            request.Headers.Remove("x-api-key");
            request.Headers.TryAddWithoutValidation("x-api-key", _apiKey);

            var response = await base.SendAsync(request, token);

            if (response.StatusCode is HttpStatusCode.Unauthorized)
            {
                response.Dispose();
                throw new UnauthorizedException();
            }

            return response;
        }, cancellationToken);
    }

    private sealed class UnauthorizedException : Exception;

    internal sealed class ConfigurationModel
    {
        /// <summary>
        ///     Secret-manager path for the Foo API key (same pattern as connection-string paths).
        /// </summary>
        public required string ApiKeyPath { get; init; }
    }
}
