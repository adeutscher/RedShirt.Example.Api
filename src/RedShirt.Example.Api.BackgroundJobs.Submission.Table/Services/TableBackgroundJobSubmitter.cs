using RedShirt.Example.Api.BackgroundJobs.Core.Services;
using RedShirt.Example.Api.BackgroundJobs.Submission.Models.Parameters;
using RedShirt.Example.Api.BackgroundJobs.Submission.Services;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Services;
using System.Text.Json;

namespace RedShirt.Example.Api.BackgroundJobs.Submission.Table.Services;

/// <summary>
///     Submits jobs to the BackgroundJob table, and wakes this process's runner so that the job starts promptly. To hand
///     jobs to an applied JobWorker instead, register an <see cref="IBackgroundJobSubmitter" /> that publishes to that
///     worker's queue.
/// </summary>
internal sealed class TableBackgroundJobSubmitter(
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

        // The row is committed by now, so the runner can lease it as soon as it wakes.
        wakeSignal.Wake();
        return model.Id;
    }
}
