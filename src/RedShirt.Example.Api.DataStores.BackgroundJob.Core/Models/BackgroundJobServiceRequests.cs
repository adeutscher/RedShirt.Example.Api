using RedShirt.Example.Api.BackgroundJobs.Submission.Core.Models;
using System.Text.Json.Serialization;

namespace RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;

public sealed class BackgroundJobServiceCreateRequest
{
    public required BackgroundJobType Type { get; init; }
    [JsonPropertyName("parameters")]
    public required string ParametersJson { get; init; }
}

public sealed class BackgroundJobServiceSearchRequest
{
    public int PageSize { get; init; }
    public BackgroundJobType? Type { get; init; }
    public BackgroundJobState? State { get; init; }

    /// <summary>
    ///     Only jobs created before this time. Pass the oldest <see cref="BackgroundJobModel.DateCreatedUtc" /> of the
    ///     previous page to get the next page.
    /// </summary>
    public DateTime? CreatedBeforeUtc { get; init; }
}

public sealed class BackgroundJobServiceLeaseRequest
{
    public required int Count { get; init; }
    public required string LeaseOwner { get; init; }
}

public sealed class BackgroundJobServiceAcknowledgeRequest
{
    public required Guid Id { get; init; }
    public required string LeaseOwner { get; init; }

    /// <summary>
    ///     The job's <see cref="BackgroundJobModel.Attempts" /> as it was leased. Each lease increments it, so it fences
    ///     the outcome to that one lease: an outcome from a lease that expired and was taken again is refused, even when
    ///     both leases had the same lease owner name.
    /// </summary>
    public required int Attempt { get; init; }

    public required BackgroundJobAttemptResult Result { get; init; }
    public string? Detail { get; init; }
}
