namespace RedShirt.Example.Api.DataStores.BackgroundJob.Core.Configuration;

/// <summary>
///     Leasing and retry settings applied by the BackgroundJob table. Environment variables use the
///     <c>BACKGROUND_JOBS__</c> prefix, shared with the runner's settings.
/// </summary>
public sealed class BackgroundJobStoreOptions
{
    public const string ConfigurationSectionName = "BackgroundJobs";

    /// <summary>
    ///     How long a leased job belongs to its lease owner, counted from the lease and again from the start report. A job
    ///     whose lease expires without an outcome report goes back to being available. Environment variable:
    ///     <c>BACKGROUND_JOBS__LEASE_SECONDS</c>.
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