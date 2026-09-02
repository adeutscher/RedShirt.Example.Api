using System.Text.Json.Serialization;

namespace RedShirt.Example.Api.Upload.Core.Events;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum UploadEventType
{
    Created,
    Completed,
    Validated,
    Rejected,
    Stored,
    Deleted
}

public sealed class UploadCreatedEvent
{
    public required Guid UploadId { get; init; }
    public required string UploadedByUserId { get; init; }
    public required string UploadedByUsername { get; init; }
    public required string UploaderIpAddress { get; init; }
    public required string FileName { get; init; }
    public required string IdempotencyKey { get; init; }
}

public sealed class UploadCompletedEvent
{
    public required Guid UploadId { get; init; }
    public required string StorageObjectKey { get; init; }
    public required string Sha256Checksum { get; init; }
}

public sealed class UploadValidatedEvent
{
    public required Guid UploadId { get; init; }
}

public sealed class UploadRejectedEvent
{
    public required Guid UploadId { get; init; }
}

public sealed class UploadStoredEvent
{
    public required Guid UploadId { get; init; }
    public required string VerifiedStorageObjectKey { get; init; }
}

public sealed class UploadDeletedEvent
{
    public required Guid UploadId { get; init; }
}