using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using RedShirt.Api.Example.Connectors.Bar.Core.Exceptions;
using RedShirt.Api.Example.Connectors.Common.Http.Enums;
using RedShirt.Api.Example.Connectors.Common.Http.Models;
using RedShirt.Api.Example.Connectors.Common.Http.Services;

namespace RedShirt.Api.Example.Connectors.Bar.Implementation.Services.Resilience;

internal interface IBarApiRequestHandlerRetryWrapperService
{
    /// <summary>
    ///     Executes <paramref name="func" /> with a one-shot retry that force-refreshes the bearer token on
    ///     <see cref="BarUnauthorizedException" />.
    /// </summary>
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> func, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Ensures a bearer access token is loaded (from OAuth token cache or provider) and returns it.
    /// </summary>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}

/// <summary>
///     Obtains Bar bearer tokens via <see cref="IOAuthTokenCache" /> and retries once on unauthorized,
///     escalating from a forced token refresh to a forced credential refresh when needed.
/// </summary>
internal sealed class BarApiRequestHandlerRetryWrapperService(
    IOAuthTokenCache oauthTokenCache,
    ILogger<BarApiRequestHandlerRetryWrapperService> logger,
    IOptions<BarApiRequestHandlerRetryWrapperService.ConfigurationModel> options)
    : IBarApiRequestHandlerRetryWrapperService
{
    private const int DefaultTokenRefreshCooldownSeconds = 60;

    /// <summary>
    ///     Gate access to token retrieval in order to avoid a stampede on the token endpoint / secret manager.
    /// </summary>
    private readonly SemaphoreSlim _tokenGate = new(1, 1);

    private string? _accessToken;
    private DateTimeOffset? _tokenFetchedAtUtc;
    private ResiliencePipeline? _retryPipeline;

    private OAuthClientCredentialsRequest CreateOAuthRequest()
    {
        var configuration = options.Value;
        return new OAuthClientCredentialsRequest
        {
            TokenUrl = configuration.TokenUrl,
            ClientIdPath = configuration.ClientIdPath,
            ClientSecretPath = configuration.ClientSecretPath,
            ScopeLabel = configuration.ScopeLabel,
            ScopeValue = configuration.ScopeValue
        };
    }

    private bool IsWithinTokenRefreshCooldown()
    {
        if (_tokenFetchedAtUtc is not { } fetchedAtUtc)
        {
            return false;
        }

        return DateTimeOffset.UtcNow < fetchedAtUtc + options.Value.EffectiveTokenRefreshCooldownSeconds;
    }

    private async Task<string> RefreshAndGetAccessTokenAsync(bool forceFreshToken, bool forceFreshCredentials,
        CancellationToken cancellationToken)
    {
        var result = await oauthTokenCache.GetAsync(CreateOAuthRequest(), forceFreshToken, forceFreshCredentials,
            cancellationToken);
        _accessToken = result.AccessToken;
        _tokenFetchedAtUtc = DateTimeOffset.UtcNow;
        return _accessToken;
    }

    private ResiliencePipeline GetRetryPipeline()
    {
        return _retryPipeline ??= new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 1,
                ShouldHandle = args => args.Outcome.Exception is BarUnauthorizedException
                    ? PredicateResult.True()
                    : PredicateResult.False(),
                DelayGenerator = static _ => new ValueTask<TimeSpan?>(TimeSpan.Zero),
                OnRetry = async args =>
                {
                    await _tokenGate.WaitAsync(args.Context.CancellationToken);
                    try
                    {
                        if (IsWithinTokenRefreshCooldown())
                        {
                            // Token was fetched recently enough to be considered stable, so cannot recover via retry.
                            throw new BarUnauthorizedException();
                        }

                        var previousAccessToken = _accessToken;
                        logger.LogDebug("Refreshing Bar bearer token from {TokenUrl}", options.Value.TokenUrl);

                        var result = await oauthTokenCache.GetAsync(CreateOAuthRequest(),
                            forceFreshToken: true,
                            forceFreshCredentials: false,
                            args.Context.CancellationToken);

                        if (string.Equals(previousAccessToken, result.AccessToken, StringComparison.Ordinal))
                        {
                            logger.LogDebug(
                                "Bar bearer token unchanged after forced token refresh; forcing credential retrieval");
                            result = await oauthTokenCache.GetAsync(CreateOAuthRequest(),
                                forceFreshToken: true,
                                forceFreshCredentials: true,
                                args.Context.CancellationToken);

                            if (string.Equals(previousAccessToken, result.AccessToken, StringComparison.Ordinal)
                                || result.TokenCacheState != TokenCacheState.ForcedCredentialRetrieval)
                            {
                                // Same token after force-refresh, so the unauthorized result cannot be recovered by retry.
                                throw new BarUnauthorizedException();
                            }
                        }

                        _accessToken = result.AccessToken;
                        _tokenFetchedAtUtc = DateTimeOffset.UtcNow;
                    }
                    finally
                    {
                        _tokenGate.Release();
                    }
                }
            })
            .Build();
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        if (_accessToken is not null)
        {
            return _accessToken;
        }

        await _tokenGate.WaitAsync(cancellationToken);
        try
        {
            if (_accessToken is not null) // Need to re-check after acquiring lock
            {
                return _accessToken;
            }

            return await RefreshAndGetAccessTokenAsync(false, false, cancellationToken);
        }
        finally
        {
            _tokenGate.Release();
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
        ///     Absolute URL of the OAuth token endpoint used for Bar bearer tokens.
        /// </summary>
        public required string TokenUrl { get; init; }

        /// <summary>
        ///     Secret-manager path for the OAuth client id.
        /// </summary>
        public required string ClientIdPath { get; init; }

        /// <summary>
        ///     Secret-manager path for the OAuth client secret.
        /// </summary>
        public required string ClientSecretPath { get; init; }

        /// <summary>
        ///     Optional form-field name used for the scope/audience-style parameter
        ///     (for example <c>scope</c> or <c>audience</c>).
        /// </summary>
        public required string? ScopeLabel { get; init; }

        /// <summary>
        ///     Optional value for <see cref="ScopeLabel" />.
        /// </summary>
        public required string? ScopeValue { get; init; }

        /// <summary>
        ///     Seconds after a token fetch during which the token is considered stable.
        ///     When still within this window, an unauthorized response is not retried.
        ///     When null, <see cref="DefaultTokenRefreshCooldownSeconds" /> is used.
        /// </summary>
        public required int? TokenRefreshCooldownSeconds { get; init; }

        /// <summary>
        ///     Effective stability window for a token fetch.
        /// </summary>
        public TimeSpan EffectiveTokenRefreshCooldownSeconds =>
            TimeSpan.FromSeconds(Math.Max(1, TokenRefreshCooldownSeconds ?? DefaultTokenRefreshCooldownSeconds));
    }
}
