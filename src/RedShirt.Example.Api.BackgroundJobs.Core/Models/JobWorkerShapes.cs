using RedShirt.Example.Api.BackgroundJobs.Submission.Core.Models;

namespace RedShirt.Example.Api.BackgroundJobs.Core.Models;

/*
 * These shapes copy the JobWorker template's Common project (RedShirt.Example.JobWorker.Common.Models and .Enums), so job
 * logic written against them moves into an applied JobWorker by copying rather than adapting. IJobDataModel is reshaped
 * for background jobs, as the JobWorker template intends an applied project to do.
 */

/// <summary>
///     Outcome of <see cref="Services.IJobLogicRunner.RunAsync" />, with the JobWorker template's meanings.
/// </summary>
public enum JobResult
{
    /// <summary>
    ///     Job application logic completed successfully.
    /// </summary>
    Success,

    /// <summary>
    ///     A recoverable failure. Handled the same as an exception: the job is tried again later until its attempts run
    ///     out.
    /// </summary>
    Failure,

    /// <summary>
    ///     The job can never succeed with its parameters. It is not retried.
    /// </summary>
    InvalidData
}

/// <summary>
///     Outcome of <see cref="Services.IJobLogicRunner.RunAsync" />.
/// </summary>
public interface IJobLogicRunnerResponse
{
    JobResult Result { get; }
}

public sealed class JobLogicRunnerResponse : IJobLogicRunnerResponse
{
    public static readonly JobLogicRunnerResponse Success = new() {Result = JobResult.Success};

    /// <summary>
    ///     Why the job failed or was refused, for the job's record. Not part of the JobWorker template's shape.
    /// </summary>
    public string? Detail { get; init; }

    public required JobResult Result { get; init; }
}

/// <summary>
///     Job data. Replaces the JobWorker template's stub <c>IJobDataModel</c>.
/// </summary>
public interface IJobDataModel
{
    BackgroundJobType Type { get; }

    /// <summary>
    ///     The job's parameters model as JSON.
    /// </summary>
    string ParametersJson { get; }
}

/// <summary>
///     Message data and metadata, as the JobWorker template's <c>IJobModel</c>.
/// </summary>
public interface IJobModel
{
    string MessageId { get; }
    string? IdempotencyId { get; }
    DateTime CreatedAtUtc { get; }
    IJobDataModel Data { get; }
}

public sealed class JobDataModel : IJobDataModel
{
    public required BackgroundJobType Type { get; init; }
    public required string ParametersJson { get; init; }
}

public sealed class JobModel : IJobModel
{
    /// <summary>
    ///     How many times the job has been leased, this time included. Not part of the JobWorker template's shape.
    /// </summary>
    public int Attempts { get; init; }

    public required string MessageId { get; init; }
    public string? IdempotencyId { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required IJobDataModel Data { get; init; }
}
