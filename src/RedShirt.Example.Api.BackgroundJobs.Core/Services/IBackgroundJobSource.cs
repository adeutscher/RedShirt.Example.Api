using RedShirt.Example.Api.BackgroundJobs.Core.Models;

namespace RedShirt.Example.Api.BackgroundJobs.Core.Services;

/// <summary>
///     Where the runner gets jobs from. A stripped-down version of the JobWorker template's <c>IJobSource</c>: fetch a
///     batch. No heartbeats and no subscriptions. There is no acknowledgement either: the job logic reports each job's
///     status to its BackgroundJob record itself, which settles the job, and a job that never reports an outcome is
///     leased again once its lease expires.
///     If your needs become complex enough, then I strongly encourage pivoting to use a JobWorker implementation.
/// </summary>
public interface IBackgroundJobSource
{
    Task<IReadOnlyList<IJobModel>> GetJobsAsync(int batchSize, CancellationToken cancellationToken = default);
}