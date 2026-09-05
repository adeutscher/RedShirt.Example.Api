using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using RedShirt.Example.Api.ClientEvents.Library.Core.Exceptions;
using RedShirt.Example.Api.Common.Services.Utility;

namespace RedShirt.Example.Api.ClientEvents.Library.Mqtt.Services.Resilience;

/// <summary>
///     Retries MQTT client-events operations that fail with expected transient exceptions,
///     then surfaces remaining failures as <see cref="ApiClientEventsException" />.
/// </summary>
internal interface IMqttClientEventsRetryWrapperService
{
    /// <summary>
    ///     Executes <paramref name="func" /> with retry for expected transient MQTT client-events failures.
    /// </summary>
    Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> func, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Executes <paramref name="func" /> with retry for expected transient MQTT client-events failures.
    /// </summary>
    Task RunAsync(Func<CancellationToken, Task> func, CancellationToken cancellationToken = default);
}

/// <summary>
///     Polly v8-based retry wrapper for MQTT client-events calls.
///     Retries when <see cref="IMqttClientEventsExceptionArbiterService" /> reports an expected transient failure,
///     using exponential backoff via <see cref="ISleepService" />.
/// </summary>
internal sealed class MqttClientEventsRetryWrapperService(
    IMqttClientEventsExceptionArbiterService exceptionArbiterService,
    ILogger<MqttClientEventsRetryWrapperService> logger,
    ISleepService sleepService,
    IOptions<MqttClientEventsRetryWrapperService.ConfigurationModel> options)
    : IMqttClientEventsRetryWrapperService
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
                        "Retrying MQTT client-events operation after attempt {AttemptNumber}",
                        args.AttemptNumber);
                    await sleepService.DelayAsync(TimeSpan.FromSeconds(Math.Pow(2, args.AttemptNumber)),
                        args.Context.CancellationToken);
                }
            })
            .Build();
    }

    private bool TryGetWrappedException(Exception exception, out Exception? wrappedException)
    {
        wrappedException = null;

        var report = exceptionArbiterService.GetReport(exception);

        if (report.AlreadyHandled && exception is ApiClientEventsException)
        {
            return false;
        }

        if (!report.IsExpected)
        {
            return false;
        }

        wrappedException = new ApiClientEventsException(exception)
        {
            CouldBeTransient = report.CouldBeTransient,
            IsHandled = true
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

            throw;
        }
    }

    internal sealed class ConfigurationModel
    {
        /// <summary>
        ///     Maximum number of retry attempts for expected transient MQTT client-events failures.
        ///     When null, <see cref="DefaultRetryCount" /> is used.
        /// </summary>
        public required int? RetryCount { get; init; }

        /// <summary>
        ///     Effective retry attempt count (floored at zero).
        /// </summary>
        public int EffectiveRetryCount => Math.Max(0, RetryCount ?? DefaultRetryCount);
    }
}
