namespace RedShirt.Example.Api.BackgroundJobs.Cleanup.Configuration;

/// <summary>
///     Settings for deleting old finished jobs from the BackgroundJob table. Environment variables use the
///     <c>BACKGROUND_JOBS__</c> prefix, shared with the other background job settings.
/// </summary>
public sealed class BackgroundJobCleanupOptions
{
    public const string ConfigurationSectionName = "BackgroundJobs";

    /// <summary>
    ///     Cleanup only runs when this is true. Environment variable: <c>BACKGROUND_JOBS__CLEANUP_ENABLED</c>.
    /// </summary>
    public bool CleanupEnabled { get; init; }

    /// <summary>
    ///     Succeeded and failed jobs that finished more than this many days ago are deleted. Required, and at least 1,
    ///     when <see cref="CleanupEnabled" /> is true. Environment variable:
    ///     <c>BACKGROUND_JOBS__CLEANUP_RETENTION_DAYS</c>.
    /// </summary>
    public int? CleanupRetentionDays { get; init; }
}
