using RedShirt.Example.Api.BackgroundJobs.Core.Models;

namespace RedShirt.Example.Api.BackgroundJobs.Core.Services;

/// <summary>
///     Handler of the actual job logic, as the JobWorker template's <c>IJobLogicRunner</c>.
/// </summary>
public interface IJobLogicRunner
{
    /// <summary>
    ///     Runs the job. Returns <see cref="JobResult.Success" />, <see cref="JobResult.Failure" /> (tried again later, as
    ///     an exception is), or <see cref="JobResult.InvalidData" /> (never retried).
    /// </summary>
    Task<IJobLogicRunnerResponse> RunAsync(IJobModel job, CancellationToken cancellationToken = default);
}
