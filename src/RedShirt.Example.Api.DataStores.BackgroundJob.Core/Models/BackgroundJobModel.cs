using RedShirt.Example.Api.BackgroundJobs.Submission.Models;

namespace RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;

public sealed class BackgroundJobModel
{
    public required Guid Id { get; init; }
    public required BackgroundJobType Type { get; init; }
    public required BackgroundJobState State { get; init; }

    /// <summary>
    ///     The job's parameters model as JSON. Its shape depends on <see cref="Type" />.
    /// </summary>
    public required string ParametersJson { get; init; }

    /// <summary>
    ///     How many times the job has been leased.
    /// </summary>
    public required int Attempts { get; init; }

    public required DateTime DateCreatedUtc { get; init; }
    public required DateTime DateUpdatedUtc { get; init; }

    /// <summary>
    ///     The job is not leased before this time. Set by retry back-off.
    /// </summary>
    public required DateTime DueAtUtc { get; init; }

    public string? LeaseOwner { get; init; }
    public DateTime? LeaseExpiresAtUtc { get; init; }
    public DateTime? CompletedAtUtc { get; init; }

    /// <summary>
    ///     Detail reported with the most recent outcome, such as why an attempt failed.
    /// </summary>
    public string? Detail { get; init; }
}

public sealed class BackgroundJobSearchResponse
{
    public required IReadOnlyList<BackgroundJobModel> Records { get; init; }
}
