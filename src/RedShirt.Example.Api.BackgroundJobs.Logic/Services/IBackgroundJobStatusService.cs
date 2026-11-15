using RedShirt.Example.Api.BackgroundJobs.Core.Models;

namespace RedShirt.Example.Api.BackgroundJobs.Logic.Services;

/// <summary>
///     How the job logic reports a job's status (start, finish, fail) to its BackgroundJob record. Inside the API, this
///     is implemented with the background job CQRS handlers. In an applied JobWorker, it would be implemented with API
///     connector methods that call the API's background job endpoints.
///     <para>
///         The job logic reports its own status, rather than the runner recording the outcome after the fact, to make a
///         migration to an applied JobWorker easier: there, the job logic has to report to the API itself, and the
///         runner's only part is handling the queue message.
///     </para>
/// </summary>
public interface IBackgroundJobStatusService
{
    /// <summary>
    ///     Reports that the job failed. With <paramref name="canRetry" /> false, the job is not tried again.
    /// </summary>
    /// <returns>False when another attempt has taken the job over. That attempt reports the outcome instead.</returns>
    Task<bool> ReportFailedAsync(IJobModel job, bool canRetry, string? detail,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Reports that the job succeeded.
    /// </summary>
    /// <returns>False when another attempt has taken the job over. That attempt reports the outcome instead.</returns>
    Task<bool> ReportFinishedAsync(IJobModel job, string? detail, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Reports that the job has started.
    /// </summary>
    /// <returns>
    ///     False when the job is no longer at this attempt (its lease expired and it was taken again) or is no longer
    ///     waiting to start. The job must then not be run.
    /// </returns>
    Task<bool> ReportStartedAsync(IJobModel job, CancellationToken cancellationToken = default);
}