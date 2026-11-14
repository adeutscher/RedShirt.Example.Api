using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Submission.Models;

namespace RedShirt.Example.Api.Models.BackgroundJob;

public sealed class BackgroundJobSearchRequest
{
    public int PageSize { get; init; }
    public BackgroundJobType? Type { get; init; }
    public BackgroundJobState? State { get; init; }

    /// <summary>
    ///     Only jobs created before this time. Pass the oldest <c>dateCreatedUtc</c> of the previous page to get the next
    ///     page.
    /// </summary>
    public DateTime? CreatedBeforeUtc { get; init; }
}

public sealed class BackgroundJobLeaseRequest
{
    /// <summary>
    ///     How many jobs to lease at most.
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    ///     Identifies the worker. Outcomes must be reported with the same lease owner.
    /// </summary>
    public required string LeaseOwner { get; init; }
}

public sealed class BackgroundJobResultRequest
{
    public required string LeaseOwner { get; init; }

    /// <summary>
    ///     The job's <c>attempts</c> value from the lease response. An outcome for any other attempt is refused with a
    ///     409, since the lease it belonged to has expired and the job was leased again.
    /// </summary>
    public required int Attempt { get; init; }

    public required JobResult Result { get; init; }
    public string? Detail { get; init; }
}
