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

    /// <summary>
    ///     Only jobs that finished (succeeded or failed) before this time. Jobs that have not finished are left out.
    /// </summary>
    public DateTime? CompletedBeforeUtc { get; init; }
}

public sealed class BackgroundJobServiceLeaseRequest
{
    public required int Count { get; init; }
    public required string LeaseOwner { get; init; }
}

/// <summary>
///     Reports that the job's leased attempt has started running.
/// </summary>
public sealed class BackgroundJobServiceStartRequest
{
    public required Guid Id { get; init; }

    /// <summary>
    ///     The job's <see cref="BackgroundJobModel.Attempts" /> as it was leased. Each lease increments it, so it fences
    ///     every status report to one lease: a report from a lease that expired and was taken again is refused.
    /// </summary>
    public required int Attempt { get; init; }
}

/// <summary>
///     Reports that the job's running attempt succeeded.
/// </summary>
public sealed class BackgroundJobServiceFinishRequest
{
    public required Guid Id { get; init; }

    /// <inheritdoc cref="BackgroundJobServiceStartRequest.Attempt" />
    public required int Attempt { get; init; }

    public string? Detail { get; init; }
}

/// <summary>
///     Reports that the job's running attempt failed.
/// </summary>
public sealed class BackgroundJobServiceFailRequest
{
    public required Guid Id { get; init; }

    /// <inheritdoc cref="BackgroundJobServiceStartRequest.Attempt" />
    public required int Attempt { get; init; }

    /// <summary>
    ///     False when no retry can fix the failure (such as unusable parameters), so the job fails right away.
    /// </summary>
    public required bool CanRetry { get; init; }

    public string? Detail { get; init; }
}
