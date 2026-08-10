using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using RedShirt.Api.Example.Connectors.Bar.Core.Exceptions;
using RedShirt.Example.Api.Common.Services.Utility;

namespace RedShirt.Api.Example.Connectors.Bar.Implementation.Services.Resilience;

/// <summary>
///     Retries Bar connector operations that fail with expected transient exceptions,
///     then surfaces remaining failures as <see cref="BarConnectorException" />.
/// </summary>
internal interface IBarRetryWrapperService
{
    /// <summary>
    ///     Executes <paramref name="func" /> with retry for expected transient Bar failures.
    /// </summary>
    Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> func, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Executes <paramref name="func" /> with retry for expected transient Bar failures.
    /// </summary>
    Task RunAsync(Func<CancellationToken, Task> func, CancellationToken cancellationToken = default);
}

/// <summary>
///     Polly v8-based retry wrapper for Bar connector calls.
///     Retries when <see cref="IBarExceptionArbiterService" /> reports an expected transient failure,
///     using exponential backoff via <see cref="ISleepService" />.
/// </summary>
internal sealed class BarRetryWrapperService(
    IBarExceptionArbiterService exceptionArbiterService,
    ILogger<BarRetryWrapperService> logger,
    ISleepService sleepService,
    IOptions<BarRetryWrapperService.ConfigurationModel> options)
    : IBarRetryWrapperService
{
    private const int DefaultRetryCount = 3;

    private ResiliencePipeline? _retryPipeline;

    private ResiliencePipeline GetRetryPipeline()
    {
        return _retryPipeline ??= new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = options.Value.EffectiveRetryCount,
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
                    return report is {IsExpected: true, CouldBeTransient: true}
                        ? PredicateResult.True()
                        : PredicateResult.False();
                },
                DelayGenerator = static _ => new ValueTask<TimeSpan?>(TimeSpan.Zero),
                OnRetry = async args =>
                {
                    logger.LogWarning(args.Outcome.Exception,
                        "Retrying Bar connector operation after attempt {AttemptNumber}",
                        args.AttemptNumber);
                    await sleepService.DelayAsync(TimeSpan.FromSeconds(Math.Pow(2, args.AttemptNumber)),
                        args.Context.CancellationToken);
                }
            })
            .Build();
    }

    private Exception WrapIfNeeded(Exception exception)
    {
        /* Handle Special Exceptions */

        // Domain not-found is a specific connector outcome. Do not wrap as BarConnectorException.
        if (exception is BarRecordNotFoundException)
        {
            return exception;
        }

        /* Handle General Exceptions */

        var report = exceptionArbiterService.GetReport(exception);

        // ReSharper disable once DuplicatedSequentialIfBodies
        if (report.AlreadyHandled && exception is BarConnectorException)
        {
            return exception;
        }

        if (!report.IsExpected)
        {
            // Throw raw unexpected exception
            return exception;
        }

        return new BarConnectorException(exception)
        {
            CouldBeTransient = report.CouldBeTransient,
            IsHandled = true,
            CouldBeExternallySolvable = report.CouldBeExternallySolvable
        };
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
            throw WrapIfNeeded(exception);
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
            throw WrapIfNeeded(exception);
        }
    }

    internal sealed class ConfigurationModel
    {
        /// <summary>
        ///     Maximum number of retry attempts for expected transient Bar failures.
        ///     When null, <see cref="DefaultRetryCount" /> is used.
        /// </summary>
        public required int? RetryCount { get; init; }

        /// <summary>
        ///     Effective retry attempt count (floored at zero).
        /// </summary>
        public int EffectiveRetryCount => Math.Max(0, RetryCount ?? DefaultRetryCount);
    }
}