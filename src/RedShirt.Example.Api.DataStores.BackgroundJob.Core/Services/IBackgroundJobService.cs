using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;

namespace RedShirt.Example.Api.DataStores.BackgroundJob.Core.Services;

/// <summary>
///     Storage operations on the BackgroundJob table. Used by the background job CQRS handlers and by the job submitter.
///     Leasing and acknowledging serve the in-API runner. Creating, reading, and searching serve every setup, including
///     one where an applied JobWorker runs the jobs.
/// </summary>
public interface IBackgroundJobService
{
    /// <summary>
    ///     Records the outcome of a leased job.
    ///     <see cref="BackgroundJobAttemptResult.Success" /> marks it succeeded,
    ///     <see cref="BackgroundJobAttemptResult.InvalidData" /> marks it failed, and
    ///     <see cref="BackgroundJobAttemptResult.Failure" /> makes it pending again after a back-off, or failed once its
    ///     attempts run out.
    /// </summary>
    Task<BackgroundJobModel> AcknowledgeAsync(BackgroundJobServiceAcknowledgeRequest request,
        CancellationToken cancellationToken = default);

    Task<BackgroundJobModel> CreateAsync(BackgroundJobServiceCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<BackgroundJobModel> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Leases up to <see cref="BackgroundJobServiceLeaseRequest.Count" /> due jobs, oldest first: pending jobs whose
    ///     due time has passed, and running jobs whose lease expired.
    /// </summary>
    Task<IReadOnlyList<BackgroundJobModel>> LeaseAsync(BackgroundJobServiceLeaseRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Makes a failed job pending again with a fresh set of attempts.
    /// </summary>
    Task<BackgroundJobModel> RetryAsync(Guid id, CancellationToken cancellationToken = default);

    Task<BackgroundJobSearchResponse> SearchAsync(BackgroundJobServiceSearchRequest request,
        CancellationToken cancellationToken = default);
}
