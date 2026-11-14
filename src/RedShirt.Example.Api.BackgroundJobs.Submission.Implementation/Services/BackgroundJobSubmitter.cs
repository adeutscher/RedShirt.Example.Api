using RedShirt.Example.Api.BackgroundJobs.Core.Services;
using RedShirt.Example.Api.BackgroundJobs.Submission.Core.Models.Parameters;
using RedShirt.Example.Api.BackgroundJobs.Submission.Core.Services;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Services;
using System.Text.Json;

namespace RedShirt.Example.Api.BackgroundJobs.Submission.Implementation.Services;

/// <summary>
///     Records each job in the BackgroundJob table, then hands it to whatever runs it. In this template that is the
///     in-API runner, which leases jobs from the table and is woken here so a job starts promptly. When job types move to
///     an applied JobWorker, the table record stays (it is the job's status), and this is where the job is enqueued for
///     the worker instead (see the marker in <see cref="SubmitAsync{TParameters}" />).
/// </summary>
internal sealed class BackgroundJobSubmitter(
    IBackgroundJobService backgroundJobService,
    IBackgroundJobWakeSignal wakeSignal) : IBackgroundJobSubmitter
{
    public async Task<Guid> SubmitAsync<TParameters>(TParameters parameters,
        CancellationToken cancellationToken = default) where TParameters : IBackgroundJobParameters
    {
        var model = await backgroundJobService.CreateAsync(new BackgroundJobServiceCreateRequest
        {
            Type = TParameters.JobType,
            ParametersJson = JsonSerializer.Serialize(parameters, BackgroundJobJson.Options)
        }, cancellationToken);

        /*
         * ENQUEUE POINT: to hand jobs to an applied JobWorker, publish the job to its queue here.
         *
         * Advice for the enqueue:
         *  * Do it after the row is created, not before, so a worker never receives a job that has no record.
         *  * Put model.Id in the message (and use it as the message's deduplication or idempotency id where the
         *    broker supports one), so the worker can use it as its idempotency id and report status against the row.
         *  * Send model.Type and the same ParametersJson, so the worker's job logic reads the parameters with
         *    BackgroundJobJson.Options exactly as the in-API handlers do.
         *  * The row and the message are two separate writes. If the publish fails, the row stays Pending with nothing
         *    to run it. Let the exception reach the caller, and either re-enqueue Pending rows that are older than a
         *    threshold on a schedule, or replace this direct publish with a transactional outbox relay that publishes
         *    Pending rows from the table.
         *
         * Once jobs go to a queue, remove the wake below and this project's BackgroundJobs.Core reference, along with
         * the in-API processing projects (BackgroundJobs.Core, BackgroundJobs.Logic, and BackgroundJobs.Runner).
         */

        // The row is committed by now, so the in-API runner can lease it as soon as it wakes.
        wakeSignal.Wake();
        return model.Id;
    }
}
