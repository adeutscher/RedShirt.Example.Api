using RedShirt.Example.Api.BackgroundJobs.Core.Models;

namespace RedShirt.Example.Api.BackgroundJobs.Core.Services;

/// <summary>
///     Where the runner gets jobs from. A stripped-down version of the JobWorker template's <c>IJobSource</c>: fetch a
///     batch, acknowledge each job. No heartbeats and no subscriptions.
/// </summary>
public interface IBackgroundJobSource
{
    Task AcknowledgeAsync(IJobModel job, IJobLogicRunnerResponse response,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IJobModel>> GetJobsAsync(int batchSize, CancellationToken cancellationToken = default);
}
