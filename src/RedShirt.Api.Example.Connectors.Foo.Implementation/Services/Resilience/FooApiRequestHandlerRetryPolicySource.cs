using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using RedShirt.Api.Example.Connectors.Foo.Implementation.Clients;
using RedShirt.Example.Api.Common.SecretManagers.Core.Services;

namespace RedShirt.Api.Example.Connectors.Foo.Implementation.Services.Resilience;

internal interface IFooApiRequestHandlerRetryPolicySource
{
    /// <summary>
    ///     Ensures an API key is loaded (from cache or secret manager) and returns it.
    /// </summary>
    Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Cached Polly v8 pipeline that force-refreshes the API key once on
    ///     <see cref="UnauthorizedException" />.
    /// </summary>
    ResiliencePipeline GetRetryPipeline();
}

/// <summary>
///     Caches the Foo API-key refresh resilience pipeline and the current API key value.
/// </summary>
internal sealed class FooApiRequestHandlerRetryPolicySource(
    ISecretManagerCacheService secretManager,
    ILogger<FooApiRequestHandlerRetryPolicySource> logger,
    IOptions<FooApiClientHandler.ConfigurationModel> options) : IFooApiRequestHandlerRetryPolicySource
{
    private string? _apiKey;
    private ResiliencePipeline? _retryPipeline;

    public async Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default)
    {
        if (_apiKey is null)
        {
            _apiKey = await secretManager.GetSecretAsync(options.Value.ApiKeyPath,
                force: false,
                cancellationToken: cancellationToken);
        }

        return _apiKey;
    }

    public ResiliencePipeline GetRetryPipeline()
    {
        return _retryPipeline ??= new ResiliencePipelineBuilder()
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
    }

    /// <summary>
    ///     Signals that the Foo API rejected the current key; the retry pipeline force-refreshes and retries.
    /// </summary>
    internal sealed class UnauthorizedException : Exception;
}
