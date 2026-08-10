using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using RedShirt.Example.Api.Common.SecretManagers.Core.Services;

namespace RedShirt.Api.Example.Connectors.Foo.Implementation.Services.Resilience;

internal interface IFooApiRequestHandlerRetryWrapperService
{
    /// <summary>
    ///     Ensures an API key is loaded (from cache or secret manager) and returns it.
    /// </summary>
    Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Executes <paramref name="func" /> with a one-shot retry that force-refreshes the API key on
    ///     <see cref="UnauthorizedException" />.
    /// </summary>
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> func, CancellationToken cancellationToken = default);
}

/// <summary>
///     Caches the Foo API-key refresh resilience pipeline and the current API key value.
/// </summary>
internal sealed class FooApiRequestHandlerRetryWrapperService(
    ISecretManagerCacheService secretManager,
    ILogger<FooApiRequestHandlerRetryWrapperService> logger,
    IOptions<FooApiRequestHandlerRetryWrapperService.ConfigurationModel> options)
    : IFooApiRequestHandlerRetryWrapperService
{
    private string? _apiKey;
    private ResiliencePipeline? _retryPipeline;

    public async Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default)
    {
        if (_apiKey is null)
        {
            await RefreshApiKeyAsync(force: false, cancellationToken);
        }

        return _apiKey!;
    }

    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> func,
        CancellationToken cancellationToken = default)
    {
        return GetRetryPipeline().ExecuteAsync(
            async token => await func(token),
            cancellationToken).AsTask();
    }

    private async Task RefreshApiKeyAsync(bool force, CancellationToken cancellationToken)
    {
        _apiKey = await secretManager.GetSecretAsync(options.Value.ApiKeyPath,
            force: force,
            cancellationToken: cancellationToken);
    }

    private ResiliencePipeline GetRetryPipeline()
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
                    await RefreshApiKeyAsync(force: true, args.Context.CancellationToken);
                }
            })
            .Build();
    }

    /// <summary>
    ///     Signals that the Foo API rejected the current key; the retry pipeline force-refreshes and retries.
    /// </summary>
    internal sealed class UnauthorizedException : Exception;

    internal sealed class ConfigurationModel
    {
        /// <summary>
        ///     Secret-manager path for the Foo API key (same pattern as connection-string paths).
        /// </summary>
        public required string ApiKeyPath { get; init; }
    }
}
