using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;

namespace RedShirt.Example.Api.DataStores.BackgroundJob.Core.Services;

/// <summary>
///     Storage operations on the BackgroundJob table. Used by the background job CQRS handlers and by the job submitter.
///     Leasing serves the in-API runner. The status reports (start, finish, fail) are made by the job logic. Creating,
///     reading, searching, and deleting serve every setup, including one where an applied JobWorker runs the jobs.
/// </summary>
public interface IBackgroundJobService
{
    Task<BackgroundJobModel> CreateAsync(BackgroundJobServiceCreateRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes a finished (succeeded or failed) job. A job that is pending or running cannot be deleted.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Reports that a running attempt failed. With attempts left and
    ///     <see cref="BackgroundJobServiceFailRequest.CanRetry" />,
    ///     the job is pending again after a back-off. Otherwise it is failed.
    /// </summary>
    Task<BackgroundJobModel> FailAsync(BackgroundJobServiceFailRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Reports that a running attempt succeeded. The job is succeeded.
    /// </summary>
    Task<BackgroundJobModel> FinishAsync(BackgroundJobServiceFinishRequest request,
        CancellationToken cancellationToken = default);

    Task<BackgroundJobModel> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Leases up to <see cref="BackgroundJobServiceLeaseRequest.Count" /> due jobs, oldest first: pending jobs whose
    ///     due time has passed and that are not leased (or whose lease expired), and running jobs whose lease expired.
    ///     Leasing counts an attempt and hands it to the lease owner, but leaves the job pending: the job logic reports
    ///     the start with <see cref="StartAsync" />. A running job whose lease expired is made pending again.
    /// </summary>
    Task<IReadOnlyList<BackgroundJobModel>> LeaseAsync(BackgroundJobServiceLeaseRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Makes a failed job pending again with a fresh set of attempts.
    /// </summary>
    Task<BackgroundJobModel> RetryAsync(Guid id, CancellationToken cancellationToken = default);

    Task<BackgroundJobSearchResponse> SearchAsync(BackgroundJobServiceSearchRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Reports that a leased attempt has started. The job is running, and its lease is renewed from now.
    /// </summary>
    Task<BackgroundJobModel> StartAsync(BackgroundJobServiceStartRequest request,
        CancellationToken cancellationToken = default);
}