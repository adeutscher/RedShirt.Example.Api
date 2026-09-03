using RedShirt.Example.Api.Upload.Core.Events;
using RedShirt.Example.Api.Upload.Core.Models.Responses;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Upload.Implementation.Services;

/// <summary>
///     No-op broadcaster for the template. In production, publish lifecycle events to AWS EventBridge,
///     Azure Event Grid, or a message queue so background workers can react without polling the API.
///     In the general template, test scripts absolutely poll the API.
/// </summary>
internal sealed class StubUploadEventBroadcaster : IUploadEventBroadcaster
{
    public Task BroadcastUploadCreatedAsync(UploadCreatedEvent uploadEvent, UploadSummaryModel summary,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task BroadcastUploadCompletedAsync(UploadCompletedEvent uploadEvent, UploadSummaryModel summary,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task BroadcastUploadValidatedAsync(UploadValidatedEvent uploadEvent, UploadSummaryModel summary,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
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