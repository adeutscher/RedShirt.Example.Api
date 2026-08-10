using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using RedShirt.Api.Example.Connectors.Foo.Core.Exceptions;
using RedShirt.Example.Api.Common.SecretManagers.Core.Services;

namespace RedShirt.Api.Example.Connectors.Foo.Implementation.Services.Resilience;

internal interface IFooApiRequestHandlerRetryWrapperService
{
    /// <summary>
    ///     Executes <paramref name="func" /> with a one-shot retry that force-refreshes the API key on
    ///     <see cref="FooUnauthorizedException" />.
    /// </summary>
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> func, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Ensures an API key is loaded (from cache or secret manager) and returns it.
    /// </summary>
    Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default);
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
    private const int DefaultApiKeyRefreshCooldownSeconds = 60;

    /// <summary>
    ///     Gate access to secret manager for API key in order to avoid a stampede on the secret manager.
    /// </summary>
    private readonly SemaphoreSlim _apiKeyGate = new(1, 1);

    private string? _apiKey;
    private DateTimeOffset? _apiKeyFetchedAtUtc;
    private ResiliencePipeline? _retryPipeline;

    private bool IsWithinApiKeyRefreshCooldown()
    {
        if (_apiKeyFetchedAtUtc is not { } fetchedAtUtc)
        {
            return false;
        }

        return DateTimeOffset.UtcNow < fetchedAtUtc + options.Value.EffectiveApiKeyRefreshCooldownSeconds;
    }

    private async Task<string> RefreshAndGetApiKeyAsync(bool force, CancellationToken cancellationToken)
    {
        var result = await secretManager.GetSecretAsync(options.Value.ApiKeyPath,
            force: force,
            cancellationToken: cancellationToken);
        _apiKey = result.Value;
        _apiKeyFetchedAtUtc = DateTimeOffset.UtcNow;
        return _apiKey;
    }

    private ResiliencePipeline GetRetryPipeline()
    {
        return _retryPipeline ??= new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 1,
                ShouldHandle = args => args.Outcome.Exception is FooUnauthorizedException
                    ? PredicateResult.True()
                    : PredicateResult.False(),
                DelayGenerator = static _ => new ValueTask<TimeSpan?>(TimeSpan.Zero),
                OnRetry = async args =>
                {
                    await _apiKeyGate.WaitAsync(args.Context.CancellationToken);
                    try
                    {
                        if (IsWithinApiKeyRefreshCooldown())
                        {
                            // Key was fetched recently enough to be considered stable, so cannot recover via retry.
                            throw new FooUnauthorizedException();
                        }

                        var previousApiKey = _apiKey;
                        logger.LogDebug("Refreshing Foo API key from secret path {ApiKeyPath}",
                            options.Value.ApiKeyPath);
                        await RefreshAndGetApiKeyAsync(true, args.Context.CancellationToken);

                        if (string.Equals(previousApiKey, _apiKey, StringComparison.Ordinal))
                        {
                            // Same key after force-refresh, so the unauthorized result cannot be recovered by retry.
                            throw new FooUnauthorizedException();
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

    public async Task<string> GetApiKeyAsync(CancellationToken cancellationToken = default)
    {
        if (_apiKey is not null)
        {
            return _apiKey;
        }

        await _apiKeyGate.WaitAsync(cancellationToken);
        try
        {
            if (_apiKey is not null) // Need to re-check after acquiring lock
            {
                return _apiKey;
            }

            return await RefreshAndGetApiKeyAsync(false, cancellationToken);
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

    internal sealed class ConfigurationModel
    {
        /// <summary>
        ///     Secret-manager path for the Foo API key (same pattern as connection-string paths).
        /// </summary>
        public required string ApiKeyPath { get; init; }

        /// <summary>
        ///     Seconds after an API key fetch during which the key is considered stable.
        ///     When still within this window, an unauthorized response is not retried.
        ///     When null, <see cref="DefaultApiKeyRefreshCooldownSeconds" /> is used.
        /// </summary>
        public required int? ApiKeyRefreshCooldownSeconds { get; init; }

        /// <summary>
        ///     Effective stability window for an API key fetch.
        /// </summary>
        public TimeSpan EffectiveApiKeyRefreshCooldownSeconds =>
            TimeSpan.FromSeconds(Math.Max(1, ApiKeyRefreshCooldownSeconds ?? DefaultApiKeyRefreshCooldownSeconds));
    }
}