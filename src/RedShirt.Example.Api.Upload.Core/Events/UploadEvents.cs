namespace RedShirt.Example.Api.Upload.Core.Events;

public static class UploadEventTypes
{
    public const string Created = "UploadCreated";
    public const string Completed = "UploadCompleted";
    public const string Validated = "UploadValidated";
    public const string Rejected = "UploadRejected";
    public const string Stored = "UploadStored";
    public const string Deleted = "UploadDeleted";
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