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
    ///     <see cref="FooApiRequestHandlerRetryWrapperService.UnauthorizedException" />.
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
    private const int DefaultApiKeyRetryCooldownSeconds = 60;

    /// <summary>
    /// Gate access to secret manager for API key in order to avoid a stampede on the secret manager.
    /// </summary>
    private readonly SemaphoreSlim _apiKeyGate = new(1, 1);

    private string? _apiKey;
    private DateTimeOffset? _apiKeyFetchedAtUtc;
    private ResiliencePipeline? _retryPipeline;

    public async Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default)
    {
        if (_apiKey is not null)
        {
            return _apiKey;
        }

        await _apiKeyGate.WaitAsync(cancellationToken);
        try
        {
            if (_apiKey is null)
            {
                await RefreshApiKeyAsync(force: false, cancellationToken);
            }

            return _apiKey!;
        }
        finally
        {
            _apiKeyGate.Release();
        }
    }

    public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> func,
        CancellationToken cancellationToken = default)
    {
        return GetRetryPipeline().ExecuteAsync(
            async token => await func(token),
            cancellationToken).AsTask();
    }

    private bool IsWithinApiKeyRetryCooldown()
    {
        if (_apiKeyFetchedAtUtc is not { } fetchedAtUtc)
        {
            return false;
        }

        return DateTimeOffset.UtcNow < fetchedAtUtc + options.Value.EffectiveApiKeyRetryCooldownSeconds;
    }

    private async Task RefreshApiKeyAsync(bool force, CancellationToken cancellationToken)
    {
        _apiKey = await secretManager.GetSecretAsync(options.Value.ApiKeyPath,
            force: force,
            cancellationToken: cancellationToken);
        _apiKeyFetchedAtUtc = DateTimeOffset.UtcNow;
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
                    await _apiKeyGate.WaitAsync(args.Context.CancellationToken);
                    try
                    {
                        // Key was fetched recently enough to be considered stable, so cannot recover via retry.
                        if (IsWithinApiKeyRetryCooldown())
                        {
                            throw new UnauthorizedException();
                        }

                        var previousApiKey = _apiKey;
                        logger.LogDebug("Refreshing Foo API key from secret path {ApiKeyPath}",
                            options.Value.ApiKeyPath);
                        await RefreshApiKeyAsync(force: true, args.Context.CancellationToken);

                        // Same key after force-refresh — unauthorized cannot be recovered by retry.
                        if (string.Equals(previousApiKey, _apiKey, StringComparison.Ordinal))
                        {
                            throw new UnauthorizedException();
                        }
                    }
                    finally
                    {
                        _apiKeyGate.Release();
                    }
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

        /// <summary>
        ///     Seconds after an API key fetch during which the key is considered stable.
        ///     When still within this window, an unauthorized response is not retried.
        ///     When null, <see cref="DefaultApiKeyRetryCooldownSeconds" /> is used.
        /// </summary>
        public required int? ApiKeyRetryCooldownSeconds { get; init; }

        /// <summary>
        ///     Effective stability window for an API key fetch.
        /// </summary>
        public TimeSpan EffectiveApiKeyRetryCooldownSeconds =>
            TimeSpan.FromSeconds(ApiKeyRetryCooldownSeconds ?? DefaultApiKeyRetryCooldownSeconds);
    }
}
