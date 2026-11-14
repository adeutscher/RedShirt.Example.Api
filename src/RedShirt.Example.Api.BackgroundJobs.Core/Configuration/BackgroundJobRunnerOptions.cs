namespace RedShirt.Example.Api.BackgroundJobs.Core.Configuration;

/// <summary>
///     Settings for the in-API background job runner. Environment variables use the <c>BACKGROUND_JOBS__</c> prefix,
///     shared with the BackgroundJob table's leasing and retry settings.
/// </summary>
public sealed class BackgroundJobRunnerOptions
{
    public const string ConfigurationSectionName = "BackgroundJobs";

    /// <summary>
    ///     When false, this API instance does not run the background job runner. Jobs are still submitted to the table and
    ///     wait there until an instance with the runner enabled picks them up.
    ///     Environment variable: <c>BACKGROUND_JOBS__RUNNER_ENABLED</c>.
    /// </summary>
    public bool RunnerEnabled { get; init; } = true;

    /// <summary>
    ///     Jobs fetched per poll, comparable to the JobWorker template's <c>JOBS__FETCH_COUNT</c>. Values below 1 are
    ///     treated as 1. Environment variable: <c>BACKGROUND_JOBS__FETCH_COUNT</c>.
    ///     <para>
    ///         Every job in a batch is leased when the batch is fetched, and the jobs then run one after another, so a
    ///         later job's lease is already running down while it waits. When more than one instance runs jobs, set this
    ///         to 1, so that each job is leased just before it runs and a waiting job cannot have its lease expire and be
    ///         taken (and run a second time) by another instance.
    ///     </para>
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
    ///     Name written as the lease owner of jobs run by this instance. Defaults to the machine name.
    ///     Environment variable: <c>BACKGROUND_JOBS__PROCESS_NAME</c>.
    /// </summary>
    public string? ProcessName { get; init; }

    public string EffectiveProcessName =>
        string.IsNullOrWhiteSpace(ProcessName) ? Environment.MachineName : ProcessName;
}
