using RedShirt.Example.Api.BackgroundJobs.Submission.Models;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;

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
