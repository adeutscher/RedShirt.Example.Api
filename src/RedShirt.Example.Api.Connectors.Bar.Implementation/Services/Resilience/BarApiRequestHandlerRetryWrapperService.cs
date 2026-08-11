using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using RedShirt.Example.Api.Connectors.Bar.Implementation.Exceptions;
using RedShirt.Example.Api.Connectors.Common.Http.Enums;
using RedShirt.Example.Api.Connectors.Common.Http.Exceptions;
using RedShirt.Example.Api.Connectors.Common.Http.Models;
using RedShirt.Example.Api.Connectors.Common.Http.Services;
using System.Net;

namespace RedShirt.Example.Api.Connectors.Bar.Implementation.Services.Resilience;

internal interface IBarApiRequestHandlerRetryWrapperService
{
    /// <summary>
    ///     Executes <paramref name="func" /> with up to two retries on <see cref="BarUnauthorizedException" />:
    ///     first forces a fresh token (or escalates to fresh credentials when still inside the token
    ///     refresh cooldown); second forces fresh credentials and a fresh token.
    /// </summary>
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> func, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Ensures a bearer access token is loaded (from OAuth token cache or provider) and returns it.
    /// </summary>
    Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default);
}

/// <summary>
///     Obtains Bar bearer tokens via <see cref="IOAuthTokenCache" /> and retries on unauthorized:
///     attempt 1 forces a fresh token (escalating to fresh credentials when inside the token refresh
///     cooldown); attempt 2 forces fresh credentials and a fresh token.
/// </summary>
internal sealed class BarApiRequestHandlerRetryWrapperService(
    IOAuthTokenCache oauthTokenCache,
    ILogger<BarApiRequestHandlerRetryWrapperService> logger,
    IOptions<BarApiRequestHandlerRetryWrapperService.ConfigurationModel> options)
    : IBarApiRequestHandlerRetryWrapperService
{
    private const int DefaultTokenRefreshCooldownSeconds = 60;

    private const string PreviousAttemptInvolvedEscalation = "e";

    /// <summary>
    ///     Gate access to token retrieval in order to avoid a stampede on the token endpoint / secret manager.
    /// </summary>
    private readonly SemaphoreSlim _tokenGate = new(1, 1);

    private HttpStatusCode? _previousAttemptStatusCode;

    private ResiliencePipeline? _retryPipeline;
    private DateTimeOffset? _tokenAttemptedAtUtc;
    private DateTimeOffset? _tokenFetchedAtUtc;

    private OAuthTokenCacheResponse? _tokenResult;

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

        return DateTimeOffset.UtcNow < fetchedAtUtc + options.Value.EffectiveTokenRefreshCooldown;
    }

    private bool IsAttemptWithinTokenRefreshCooldown()
    {
        if (_tokenAttemptedAtUtc is not { } attemptedAtUtc)
        {
            return false;
        }

        return DateTimeOffset.UtcNow < attemptedAtUtc + options.Value.EffectiveTokenRefreshCooldown;
    }

    private async Task<OAuthTokenCacheResponse> RefreshAndGetAccessTokenAsync(bool forceFreshToken,
        bool forceFreshCredentials,
        CancellationToken cancellationToken)
    {
        if (!forceFreshCredentials
            && _previousAttemptStatusCode != HttpStatusCode.OK
            && IsAttemptWithinTokenRefreshCooldown())
        {
            // Assume unauthorized
            throw new BarUnavailableException();
        }

        _tokenAttemptedAtUtc = DateTimeOffset.UtcNow;
        OAuthTokenCacheResponse result;
        try
        {
            result = await oauthTokenCache.GetAsync(CreateOAuthRequest(), forceFreshToken, forceFreshCredentials,
                cancellationToken);
            _previousAttemptStatusCode = HttpStatusCode.OK;
        }
        catch (OAuthRequestException e)
        {
            _previousAttemptStatusCode = e.StatusCode;
            throw;
        }

        _tokenResult = result;
        _tokenFetchedAtUtc = DateTimeOffset.UtcNow;
        return result;
    }

    private ResiliencePipeline GetRetryPipeline()
    {
        return _retryPipeline ??= new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 2, // Retry maximum of twice
                ShouldHandle = args =>
                {
                    /*
                     * An expected exception here suggests that either:
                     *  * Token source threw a OAuthRequestException (HTTP 401), suggesting a bad secret
                     *  * Handler threw a BarUnauthorizedException, suggesting an expired token
                     */

                    /*
                     * An OAuthRequestException can only be handled if instigating incident was on the first attempt
                     * (Polly v8 marks first attempt as 0)
                     * did not involve a forced credentials pull from the secret manager.
                     */
                    // ReSharper disable once ConvertIfStatementToSwitchStatement
                    if (args.AttemptNumber == 0
                        // ReSharper disable once MergeIntoPattern
                        && args.Outcome.Exception is OAuthRequestException
                        {
                            StatusCode: HttpStatusCode.Unauthorized,
                            CredentialStorageProblem: false,
                            FreshCredentialCacheResult: false
                        })
                    {
                        return PredicateResult.True();
                    }

                    // ReSharper disable once ConvertIfStatementToReturnStatement
                    if (args.Outcome.Exception is BarUnauthorizedException
                        && !IsWithinTokenRefreshCooldown()
                        && !(
                            args.Context.Properties.TryGetValue(
                                new ResiliencePropertyKey<bool>(PreviousAttemptInvolvedEscalation),
                                out var previousAttemptInvolvedEscalation)
                            && previousAttemptInvolvedEscalation
                        )
                       )
                    {
                        // A BarUnauthorizedException suggests that the HTTP handler for the remote API returned a 401. 
                        return PredicateResult.True();
                    }

                    return PredicateResult.False();
                },
                DelayGenerator = static _ => new ValueTask<TimeSpan?>(TimeSpan.Zero),
                OnRetry = async args =>
                {
                    await _tokenGate.WaitAsync(args.Context.CancellationToken);
                    try
                    {
                        /*
                         * Force if one of the following is true:
                         *  * Second attempt (Polly v8 starts at 0, so attempt '1' is the second one)
                         *  * Previous attempt was already to OAuth (suggesting a problem during the client's first attempt)
                         *      * OAuth properties were already handled during ShouldHandle
                         */
                        var forceFreshCredentials = args.AttemptNumber >= 1
                                                    || args.Outcome.Exception is OAuthRequestException;

                        var previousAccessToken = _tokenResult?.AccessToken;
                        logger.LogDebug(
                            "Refreshing Bar bearer token from {TokenUrl} (forceFreshCredentials: {ForceFreshCredentials})",
                            options.Value.TokenUrl, forceFreshCredentials);

                        OAuthTokenCacheResponse result;
                        try
                        {
                            result = await RefreshAndGetAccessTokenAsync(true, forceFreshCredentials,
                                args.Context.CancellationToken);
                        }
                        catch (OAuthRequestException) when (!forceFreshCredentials)
                        {
                            // Stale client credentials are still cached
                            // Trying to escalate to forced credentials immediately. The alternative is
                            // spending time on another call to the HttpHandler that we are almost certain will splatter
                            args.Context.Properties.Set(
                                new ResiliencePropertyKey<bool>(PreviousAttemptInvolvedEscalation), true);
                            result = await RefreshAndGetAccessTokenAsync(true, true, args.Context.CancellationToken);
                        }

                        // ReSharper disable once ConvertIfStatementToSwitchStatement
                        if (forceFreshCredentials
                            && (string.Equals(previousAccessToken, result.AccessToken, StringComparison.Ordinal)
                                || result.TokenCacheState != TokenCacheState.ForcedCredentialRetrieval))
                        {
                            // Same token after forced credential refresh, so unauthorized cannot be recovered.
                            throw new BarUnauthorizedException();
                        }
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
        if (_tokenResult is not null)
        {
            return _tokenResult.AccessToken;
        }

        await _tokenGate.WaitAsync(cancellationToken);
        try
        {
            if (_tokenResult is not null) // Need to re-check after acquiring lock
            {
                return _tokenResult.AccessToken;
            }

            await RefreshAndGetAccessTokenAsync(false, false, cancellationToken);
            return _tokenResult!.AccessToken;
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
        public TimeSpan EffectiveTokenRefreshCooldown =>
            TimeSpan.FromSeconds(Math.Max(1, TokenRefreshCooldownSeconds ?? DefaultTokenRefreshCooldownSeconds));
    }
}