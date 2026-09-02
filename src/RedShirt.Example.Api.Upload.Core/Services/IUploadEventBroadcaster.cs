using RedShirt.Example.Api.Upload.Core.Events;
using RedShirt.Example.Api.Upload.Core.Models.Responses;

namespace RedShirt.Example.Api.Upload.Core.Services;

/// <summary>
///     Broadcasts upload lifecycle signals to external infrastructure.
///     The implementation is intended to publish to AWS EventBridge, Azure Event Grid, a message queue, or similar.
/// </summary>
public interface IUploadEventBroadcaster
{
    Task BroadcastUploadCompletedAsync(UploadCompletedEvent uploadEvent, UploadSummaryModel summary,
        CancellationToken cancellationToken = default);

    Task BroadcastUploadCreatedAsync(UploadCreatedEvent uploadEvent, UploadSummaryModel summary,
        CancellationToken cancellationToken = default);

    Task BroadcastUploadDeletedAsync(UploadDeletedEvent uploadEvent, UploadSummaryModel summary,
        CancellationToken cancellationToken = default);

    Task BroadcastUploadPurgedAsync(UploadPurgedEvent uploadEvent,
        CancellationToken cancellationToken = default);

    Task BroadcastUploadRejectedAsync(UploadRejectedEvent uploadEvent, UploadSummaryModel summary,
        CancellationToken cancellationToken = default);

    Task BroadcastUploadStoredAsync(UploadStoredEvent uploadEvent, UploadSummaryModel summary,
        CancellationToken cancellationToken = default);

    Task BroadcastUploadValidatedAsync(UploadValidatedEvent uploadEvent, UploadSummaryModel summary,
        CancellationToken cancellationToken = default);
}