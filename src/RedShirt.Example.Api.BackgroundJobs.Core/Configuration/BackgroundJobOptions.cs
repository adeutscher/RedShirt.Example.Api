namespace RedShirt.Example.Api.BackgroundJobs.Core.Configuration;

/// <summary>
///     Settings for the in-API background job system. Environment variables use the <c>BACKGROUND_JOBS__</c> prefix.
/// </summary>
public sealed class BackgroundJobOptions
{
    public const string ConfigurationSectionName = "BackgroundJobs";

    /// <summary>
    ///     When false, this API instance does not run the background job runner. Jobs are still submitted to the table, so
    ///     another process (such as a JobWorker reading through the background job endpoints) can run them.
    ///     Environment variable: <c>BACKGROUND_JOBS__RUNNER_ENABLED</c>.
    /// </summary>
    public bool RunnerEnabled { get; init; } = true;

    /// <summary>
    ///     Jobs fetched per poll, comparable to the JobWorker template's <c>JOBS__FETCH_COUNT</c>.
    ///     Environment variable: <c>BACKGROUND_JOBS__FETCH_COUNT</c>.
    /// </summary>
    public int FetchCount { get; init; } = 5;

    public int EffectiveFetchCount => Math.Max(1, FetchCount);

    /// <summary>
    ///     Upper bound of the idle back-off between empty polls. Environment variable:
    ///     <c>BACKGROUND_JOBS__MAX_IDLE_WAIT_SECONDS</c>.
    /// </summary>
    public int MaxIdleWaitSeconds { get; init; } = 30;

    public int EffectiveMaxIdleWaitSeconds => Math.Max(1, MaxIdleWaitSeconds);

    /// <summary>
    ///     How long a leased job belongs to its lease owner. A job whose lease expires without an acknowledgement goes back
    ///     to being available. Environment variable: <c>BACKGROUND_JOBS__LEASE_SECONDS</c>.
    /// </summary>
    public int LeaseSeconds { get; init; } = 300;

    public int EffectiveLeaseSeconds => Math.Max(1, LeaseSeconds);

    /// <summary>
    ///     After this many attempts, a job that keeps failing is marked as failed instead of being retried.
    ///     Environment variable: <c>BACKGROUND_JOBS__MAX_ATTEMPTS</c>.
    /// </summary>
    public int MaxAttempts { get; init; } = 5;

    public int EffectiveMaxAttempts => Math.Max(1, MaxAttempts);

    /// <summary>
    ///     Delay before the first retry of a failed attempt. Doubles on each further attempt, up to
    ///     <see cref="MaxRetryDelaySeconds" />. Environment variable: <c>BACKGROUND_JOBS__RETRY_DELAY_SECONDS</c>.
    /// </summary>
    public int RetryDelaySeconds { get; init; } = 15;

    /// <summary>
    ///     Environment variable: <c>BACKGROUND_JOBS__MAX_RETRY_DELAY_SECONDS</c>.
    /// </summary>
    public int MaxRetryDelaySeconds { get; init; } = 600;

    /// <summary>
    ///     Name written as the lease owner of jobs run by this instance. Defaults to the machine name.
    ///     Environment variable: <c>BACKGROUND_JOBS__PROCESS_NAME</c>.
    /// </summary>
    public string? ProcessName { get; init; }

    public string EffectiveProcessName =>
        string.IsNullOrWhiteSpace(ProcessName) ? Environment.MachineName : ProcessName;

    /// <summary>
    ///     Retry delay after <paramref name="attempts" /> attempts.
    /// </summary>
    public TimeSpan GetRetryDelay(int attempts)
    {
        var baseSeconds = Math.Max(0, RetryDelaySeconds);
        var maxSeconds = Math.Max(baseSeconds, MaxRetryDelaySeconds);
        var exponent = Math.Clamp(attempts - 1, 0, 30);
        var seconds = Math.Min(maxSeconds, baseSeconds * Math.Pow(2, exponent));
        return TimeSpan.FromSeconds(seconds);
    }
}
