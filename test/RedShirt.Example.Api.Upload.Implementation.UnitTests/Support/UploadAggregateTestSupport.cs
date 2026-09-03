using RedShirt.Example.Api.Upload.Core.Events;
using RedShirt.Example.Api.Upload.Implementation.Aggregates;
using RedShirt.Example.Api.Upload.Implementation.Entities;
using System.Text.Json;

namespace RedShirt.Example.Api.Upload.Implementation.UnitTests.Support;

internal static class UploadAggregateTestSupport
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    internal static (UploadEventType EventType, object Payload) Completed(
        Guid uploadId,
        string storageObjectKey,
        string sha256 = "abc123")
    {
        return (UploadEventType.Completed, new UploadCompletedEvent
        {
            UploadId = uploadId,
            StorageObjectKey = storageObjectKey,
            Sha256Checksum = sha256
        });
    }

    internal static (UploadEventType EventType, object Payload) Created(
        Guid uploadId,
        string uploadedByUserId = "user-id",
        string fileName = "file.txt")
    {
        return (UploadEventType.Created, new UploadCreatedEvent
        {
            UploadId = uploadId,
            UploadedByUserId = uploadedByUserId,
            UploadedByUsername = "user",
            UploaderIpAddress = "203.0.113.10",
            FileName = fileName,
            IdempotencyKey = "idem-key"
        });
    }

    internal static UploadAggregate Rehydrate(
        Guid uploadId,
        params (UploadEventType EventType, object Payload)[] events)
    {
        var baseTime = DateTime.UtcNow;
        var entities = events.Select((entry, index) => new UploadEventEntity
        {
            Id = Guid.NewGuid(),
            UploadId = uploadId,
            EventDateUtc = baseTime.AddSeconds(index),
            EventType = entry.EventType,
            Json = JsonSerializer.Serialize(entry.Payload, JsonOptions)
        }).ToList();

        return UploadAggregate.FromEvents(entities);
    }

    internal static (UploadEventType EventType, object Payload) Stored(
        Guid uploadId,
        string verifiedStorageObjectKey)
    {
        return (UploadEventType.Stored, new UploadStoredEvent
        {
            UploadId = uploadId,
            VerifiedStorageObjectKey = verifiedStorageObjectKey
        });
    }

    internal static (UploadEventType EventType, object Payload) Validated(Guid uploadId)
    {
        return (UploadEventType.Validated, new UploadValidatedEvent {UploadId = uploadId});
    }
}