using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using RedShirt.Example.Api.Common.Services.Utility;
using RedShirt.Example.Api.Connectors.Foo.Core.Exceptions;

namespace RedShirt.Example.Api.Connectors.Foo.Implementation.Services.Resilience;

/// <summary>
///     Retries Foo connector operations that fail with expected transient exceptions,
///     then surfaces remaining failures as <see cref="FooConnectorException" />.
/// </summary>
internal interface IFooRetryWrapperService
{
    /// <summary>
    ///     Executes <paramref name="func" /> with retry for expected transient Foo failures.
    /// </summary>
    Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> func, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Executes <paramref name="func" /> with retry for expected transient Foo failures.
    /// </summary>
    Task RunAsync(Func<CancellationToken, Task> func, CancellationToken cancellationToken = default);
}

/// <summary>
///     Polly v8-based retry wrapper for Foo connector calls.
///     Retries when <see cref="IFooExceptionArbiterService" /> reports an expected transient failure.
///     Waits exactly as long as a <see cref="FooConnectorException.RetryAfter" /> hint asks, otherwise uses
///     exponential backoff with jitter via <see cref="ISleepService" />.
/// </summary>
internal sealed class FooRetryWrapperService(
    IFooExceptionArbiterService exceptionArbiterService,
    ILogger<FooRetryWrapperService> logger,
    ISleepService sleepService,
    IOptions<FooRetryWrapperService.ConfigurationModel> options)
    : IFooRetryWrapperService
{
    private const int DefaultRetryCount = 3;

    private const int DefaultMaxRetryAfterSeconds = 30;

    private const double MaximumJitter = 0.2;

    private static readonly ResiliencePropertyKey<int> RetriesUsedKey = new("retriesUsed");

    private static readonly ResiliencePropertyKey<TimeSpan> NextDelayKey = new("nextDelay");

    private ResiliencePipeline? _retryPipeline;

    private ResiliencePipeline GetRetryPipeline()
    {
        return _retryPipeline ??= new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                // The retry budget is enforced in ShouldHandle, so that throttled calls can be exempt from it.
                MaxRetryAttempts = int.MaxValue,
                ShouldHandle = args =>
                {
                    // ReSharper disable once DuplicatedSequentialIfBodies
                    if (args.Outcome.Exception is not { } exception)
                    {
                        return PredicateResult.False();
                    }

                    if (args.Context.CancellationToken.IsCancellationRequested)
                    {
                        return PredicateResult.False();
                    }

                    var report = exceptionArbiterService.GetReport(exception);
                    if (report is not {IsExpected: true, CouldBeTransient: true})
                    {
                        return PredicateResult.False();
                    }

                    var retriesUsed = args.Context.Properties.GetValue(RetriesUsedKey, 0);
                    var delay = GetRetryDelay(exception, ref retriesUsed);
                    if (delay is null)
                    {
                        return PredicateResult.False();
                    }

                    args.Context.Properties.Set(RetriesUsedKey, retriesUsed);
                    args.Context.Properties.Set(NextDelayKey, delay.Value);
                    return PredicateResult.True();
                },
                DelayGenerator = static _ => new ValueTask<TimeSpan?>(TimeSpan.Zero),
                OnRetry = async args =>
                {
                    var delay = args.Context.Properties.GetValue(NextDelayKey, TimeSpan.Zero);
                    logger.LogWarning(args.Outcome.Exception,
                        "Retrying Foo connector operation after attempt {AttemptNumber} in {Delay}",
                        args.AttemptNumber, delay);
                    await sleepService.DelayAsync(delay, args.Context.CancellationToken);
                }
            })
            .Build();
    }

    /// <summary>
    ///     The wait before the next attempt, or null to stop retrying.
    /// </summary>
    /// <param name="exception">The transient exception from the latest attempt.</param>
    /// <param name="retriesUsed">Retries counted against the budget so far. Incremented when a retry is counted.</param>
    private TimeSpan? GetRetryDelay(Exception exception, ref int retriesUsed)
    {
        var configuration = options.Value;

        if (exception is FooConnectorException {RetryAfter: { } requested} hinted)
        {
            if (configuration.EffectiveMaxRetryAfter is { } maximum && requested > maximum)
            {
                // Too long to hold the caller. The caller can still read the hint from the thrown exception.
                return null;
            }

            if (!(hinted.IsThrottled && configuration.RetryThrottledPerpetually))
            {
                if (retriesUsed >= configuration.EffectiveRetryCount)
                {
                    return null;
                }

                retriesUsed++;
            }

            // The dependency named the wait, so it gets no jitter.
            return requested;
        }

        if (retriesUsed >= configuration.EffectiveRetryCount)
        {
            return null;
        }

        var backoff = TimeSpan.FromSeconds(Math.Pow(2, retriesUsed));
        retriesUsed++;

        // Jitter keeps callers that failed together from retrying together.
        return backoff * (1 + Random.Shared.NextDouble() * MaximumJitter);
    }

    /// <summary>
    ///     Try to get the wrapped exception.
    /// </summary>
    /// <param name="exception">Exception to be judged.</param>
    /// <param name="wrappedException">
    ///     If wrapping was appropriate, then will be <see cref="FooConnectorException" /> wrapped around the
    ///     <paramref name="exception" />.
    ///     If wrapping was not appropriate, then will be <c>null</c>.
    /// </param>
    /// <returns><c>true</c> if the exception was wrapped, else <c>false</c></returns>
    private bool TryGetWrappedException(Exception exception, out Exception? wrappedException)
    {
        wrappedException = null;

        /* Handle Special Exceptions */

        // Domain not-found is a specific connector outcome. Do not wrap as FooConnectorException.
        if (exception is FooRecordNotFoundException)
        {
            return false;
        }

        // Raised by the client for this wrapper to retry. Retries are now done, so mark it handled.
        if (exception is FooConnectorException {IsHandled: false} unhandled)
        {
            wrappedException = new FooConnectorException(unhandled.InnerException ?? unhandled)
            {
                CouldBeTransient = unhandled.CouldBeTransient,
                IsHandled = true,
                CouldBeExternallySolvable = unhandled.CouldBeExternallySolvable,
                IsThrottled = unhandled.IsThrottled,
                RetryAfter = unhandled.RetryAfter
            };
            return true;
        }

        /* Handle General Exceptions */

        var report = exceptionArbiterService.GetReport(exception);

        // ReSharper disable once DuplicatedSequentialIfBodies
        if (report.AlreadyHandled && exception is FooConnectorException)
        {
            return false;
        }

        if (!report.IsExpected)
        {
            // Throw raw unexpected exception
            return false;
        }

        wrappedException = new FooConnectorException(exception)
        {
            CouldBeTransient = report.CouldBeTransient,
            IsHandled = true,
            CouldBeExternallySolvable = report.CouldBeExternallySolvable
        };
        return true;
    }

    public async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> func,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetRetryPipeline().ExecuteAsync(
                async token => await func(token),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (TryGetWrappedException(exception, out var wrappedException) && wrappedException is not null)
            {
                throw wrappedException;
            }

            // Do a flat throw to preserve stack trace
            throw;
        }
    }

    public async Task RunAsync(Func<CancellationToken, Task> func, CancellationToken cancellationToken = default)
    {
        try
        {
            await GetRetryPipeline().ExecuteAsync(
                async token => await func(token),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            if (TryGetWrappedException(exception, out var wrappedException) && wrappedException is not null)
            {
                throw wrappedException;
            }

            // Do a flat throw to preserve stack trace
            throw;
        }
    }

    internal sealed class ConfigurationModel
    {
        /// <summary>
        ///     Maximum number of retry attempts for expected transient Foo failures.
        ///     When null, <see cref="DefaultRetryCount" /> is used.
        /// </summary>
        public required int? RetryCount { get; init; }

        /// <summary>
        ///     Longest <see cref="FooConnectorException.RetryAfter" /> hint that is waited out before retrying.
        ///     A longer hint stops retrying at once. Zero or less accepts any hint.
        ///     When null, <see cref="DefaultMaxRetryAfterSeconds" /> is used.
        /// </summary>
        public required int? MaxRetryAfterSeconds { get; init; }

        /// <summary>
        ///     When <c>true</c>, a throttled failure with a <see cref="FooConnectorException.RetryAfter" /> hint does not
        ///     use up <see cref="RetryCount" />, so a call keeps retrying while Foo keeps throttling.
        ///     Only the caller's cancellation token ends it.
        /// </summary>
        public required bool RetryThrottledPerpetually { get; init; }

        /// <summary>
        ///     Effective retry attempt count (floored at zero).
        /// </summary>
        public int EffectiveRetryCount => Math.Max(0, RetryCount ?? DefaultRetryCount);

        /// <summary>
        ///     Effective cap on a retry hint, or null when any hint is accepted.
        /// </summary>
        public TimeSpan? EffectiveMaxRetryAfter =>
            (MaxRetryAfterSeconds ?? DefaultMaxRetryAfterSeconds) is var seconds and > 0
                ? TimeSpan.FromSeconds(seconds)
                : null;
    }
}