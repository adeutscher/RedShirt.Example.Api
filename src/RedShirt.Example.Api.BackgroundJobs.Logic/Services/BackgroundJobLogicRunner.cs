using Microsoft.Extensions.Logging;
using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Core.Services;
using RedShirt.Example.Api.BackgroundJobs.Logic.Handlers;

namespace RedShirt.Example.Api.BackgroundJobs.Logic.Services;

/// <summary>
///     Picks the handler for a job's type and runs it. This is the class an applied JobWorker's Core.Logic project would
///     register as its <see cref="IJobLogicRunner" />.
/// </summary>
internal sealed class BackgroundJobLogicRunner(
    IEnumerable<IBackgroundJobHandler> handlers,
    ILogger<BackgroundJobLogicRunner> logger) : IJobLogicRunner
{
    private readonly Dictionary<BackgroundJobType, IBackgroundJobHandler> _handlers =
        handlers.ToDictionary(handler => handler.Type);

    public Task<IJobLogicRunnerResponse> RunAsync(IJobModel job, CancellationToken cancellationToken = default)
    {
        if (!_handlers.TryGetValue(job.Data.Type, out var handler))
        {
            return Task.FromResult<IJobLogicRunnerResponse>(new JobLogicRunnerResponse
            {
                Result = JobResult.InvalidData,
                Detail = $"No handler is registered for job type {job.Data.Type}."
            });
        }

        logger.LogDebug("Running {JobType} job {MessageId}", job.Data.Type, job.MessageId);
        return handler.HandleAsync(job, cancellationToken);
    }
}
