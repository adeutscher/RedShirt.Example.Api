using Microsoft.Extensions.Logging;
using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Core.Services;
using RedShirt.Example.Api.BackgroundJobs.Logic.Handlers;
using RedShirt.Example.Api.BackgroundJobs.Submission.Core.Models;

namespace RedShirt.Example.Api.BackgroundJobs.Logic.Services;

/// <summary>
///     Picks the handler for a job's type and runs it. This is the class an applied JobWorker's Core.Logic project would
///     register as its <see cref="IJobLogicRunner" />.
/// </summary>
internal sealed class BackgroundJobLogicRunner(
    IEnumerable<IBackgroundJobHandler> handlers,
    IBackgroundJobStatusService statusService,
    ILogger<BackgroundJobLogicRunner> logger) : IJobLogicRunner
{
    private readonly Dictionary<BackgroundJobType, IBackgroundJobHandler> _handlers =
        handlers.ToDictionary(handler => handler.Type);

    public async Task<IJobLogicRunnerResponse> RunAsync(IJobModel job, CancellationToken cancellationToken = default)
    {
        if (_handlers.TryGetValue(job.Data.Type, out var handler))
        {
            logger.LogDebug("Running {JobType} job {MessageId}", job.Data.Type, job.MessageId);
            // The handler reports the job's status itself.
            return await handler.HandleAsync(job, cancellationToken);
        }

        var response = new JobLogicRunnerResponse
        {
            Result = JobResult.InvalidData,
            Detail = $"No handler is registered for job type {job.Data.Type}."
        };

        /*
         * With no handler to report the job's status, it is reported here, so the job fails instead of waiting out its
         * attempts. These reports go to the background job CQRS handlers inside the API. If this logic runner is
         * migrated to an applied JobWorker, they need to be replaced with API connector methods (see
         * IBackgroundJobStatusService).
         */
        if (await statusService.ReportStartedAsync(job, cancellationToken))
        {
            await statusService.ReportFailedAsync(job, false, response.Detail, cancellationToken);
        }

        return response;
    }
}
