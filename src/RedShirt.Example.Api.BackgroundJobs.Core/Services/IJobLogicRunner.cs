using RedShirt.Example.Api.BackgroundJobs.Core.Models;

namespace RedShirt.Example.Api.BackgroundJobs.Core.Services;

/// <summary>
///     Handler of the actual job logic, as the JobWorker template's <c>IJobLogicRunner</c>.
/// </summary>
public interface IJobLogicRunner
{
    /// <summary>
    ///     Runs the job and reports its status to its BackgroundJob record. Returns <see cref="JobResult.Success" />,
    ///     <see cref="JobResult.Failure" /> (tried again later, as an exception is), or
    ///     <see cref="JobResult.InvalidData" /> (never retried). The returned value is only for handling the job's message
    ///     in the messaging service, as in an applied JobWorker. The in-API runner has no message to handle, so it only
    ///     logs it.
    /// </summary>
    Task<IJobLogicRunnerResponse> RunAsync(IJobModel job, CancellationToken cancellationToken = default);
}