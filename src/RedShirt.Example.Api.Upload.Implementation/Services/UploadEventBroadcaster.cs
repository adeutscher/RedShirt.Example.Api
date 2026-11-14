using RedShirt.Example.Api.BackgroundJobs.Submission.Core.Models.Parameters;
using RedShirt.Example.Api.BackgroundJobs.Submission.Core.Services;
using RedShirt.Example.Api.Upload.Core.Events;
using RedShirt.Example.Api.Upload.Core.Models.Responses;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Upload.Implementation.Services;

/// <summary>
///     Turns upload lifecycle events into background jobs. A completed upload gets a
///     <see cref="ValidateUploadJobParameters" /> job, and a validated upload gets a <see cref="MoveUploadJobParameters" />
///     job. The other events need no background work in this template.
///     <para>
///         Jobs are sent through <see cref="IBackgroundJobSubmitter" />, so they go to the in-API BackgroundJob table or to
///         a JobWorker's queue depending on which submitter is registered. An applied project could also publish these
///         events to AWS EventBridge, Azure Event Grid, or similar for other consumers.
///     </para>
/// </summary>
internal sealed class UploadEventBroadcaster(IBackgroundJobSubmitter backgroundJobSubmitter) : IUploadEventBroadcaster
{
    public Task BroadcastUploadCreatedAsync(UploadCreatedEvent uploadEvent, UploadSummaryModel summary,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public async Task BroadcastUploadCompletedAsync(UploadCompletedEvent uploadEvent, UploadSummaryModel summary,
        CancellationToken cancellationToken = default)
    {
        /*
         * The job is submitted after the Completed event has been saved, not in the same transaction. If submission
         * fails, the upload stays NotValidated and the caller sees the error. A verdict can still be submitted by hand
         * through POST /uploads/{id}/verdicts.
         */
        await backgroundJobSubmitter.SubmitAsync(new ValidateUploadJobParameters
        {
            UploadId = uploadEvent.UploadId
        }, cancellationToken);
    }

    public async Task BroadcastUploadValidatedAsync(UploadValidatedEvent uploadEvent, UploadSummaryModel summary,
        CancellationToken cancellationToken = default)
    {
        // As with validation: if submission fails, the upload stays Verified, and a move can still be reported by hand
        // through POST /uploads/{id}/move-reports.
        await backgroundJobSubmitter.SubmitAsync(new MoveUploadJobParameters
        {
            UploadId = uploadEvent.UploadId
        }, cancellationToken);
    }

    public Task BroadcastUploadRejectedAsync(UploadRejectedEvent uploadEvent, UploadSummaryModel summary,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task BroadcastUploadStoredAsync(UploadStoredEvent uploadEvent, UploadSummaryModel summary,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task BroadcastUploadDeletedAsync(UploadDeletedEvent uploadEvent, UploadSummaryModel summary,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task BroadcastUploadPurgedAsync(UploadPurgedEvent uploadEvent,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
