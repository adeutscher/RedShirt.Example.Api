using RedShirt.Example.Api.Upload.Core.Events;
using RedShirt.Example.Api.Upload.Core.Models;
using RedShirt.Example.Api.Upload.Core.Models.Responses;
using RedShirt.Example.Api.Upload.Implementation.Entities;
using System.Text.Json;

namespace RedShirt.Example.Api.Upload.Implementation.Aggregates;

/// <summary>
///     Event-sourced upload aggregate using a Marten-style <c>Apply</c> pattern for rehydration.
/// </summary>
internal sealed class UploadAggregate
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private string UploadedByUsername { get; set; } = string.Empty;
    private string UploaderIpAddress { get; set; } = string.Empty;
    private DateTime? DateCompletedUtc { get; set; }
    private DateTime? DateValidatedUtc { get; set; }
    private DateTime? DateRejectedUtc { get; set; }
    private DateTime? DateStoredUtc { get; set; }
    private DateTime? DateDeletedUtc { get; set; }

    private void Apply(UploadCreatedEvent uploadEvent, DateTime eventDateUtc)
    {
        Id = uploadEvent.UploadId;
        UploadedByUserId = uploadEvent.UploadedByUserId;
        UploadedByUsername = uploadEvent.UploadedByUsername;
        UploaderIpAddress = uploadEvent.UploaderIpAddress;
        FileName = uploadEvent.FileName;
        IdempotencyKey = uploadEvent.IdempotencyKey;
        DateCreatedUtc = eventDateUtc;
        DateUpdatedUtc = eventDateUtc;
        State = UploadState.Uploading;
    }

    private void Apply(UploadCompletedEvent uploadEvent, DateTime eventDateUtc)
    {
        StorageObjectKey = uploadEvent.StorageObjectKey;
        Sha256Checksum = uploadEvent.Sha256Checksum;
        FileSizeBytes = uploadEvent.FileSizeBytes;
        DateCompletedUtc = eventDateUtc;
        DateUpdatedUtc = eventDateUtc;
        State = UploadState.NotValidated;
    }

    private void Apply(UploadValidatedEvent uploadEvent, DateTime eventDateUtc)
    {
        IsValidated = true;
        DateValidatedUtc = eventDateUtc;
        DateUpdatedUtc = eventDateUtc;
        State = UploadState.Verified;
    }

    private void Apply(UploadRejectedEvent uploadEvent, DateTime eventDateUtc)
    {
        IsRejected = true;
        DateRejectedUtc = eventDateUtc;
        DateUpdatedUtc = eventDateUtc;
        State = UploadState.Rejected;
    }

    private void Apply(UploadStoredEvent uploadEvent, DateTime eventDateUtc)
    {
        VerifiedStorageObjectKey = uploadEvent.VerifiedStorageObjectKey;
        DateStoredUtc = eventDateUtc;
        DateUpdatedUtc = eventDateUtc;
        State = UploadState.Stored;
    }

    private void Apply(UploadDeletedEvent uploadEvent, DateTime eventDateUtc)
    {
        DateDeletedUtc = eventDateUtc;
        DateUpdatedUtc = eventDateUtc;
        State = UploadState.Deleted;
    }

    private void ApplyEvent(UploadEventType eventType, string json, DateTime eventDateUtc)
    {
        switch (eventType)
        {
            case UploadEventType.Created:
                Apply(JsonSerializer.Deserialize<UploadCreatedEvent>(json, JsonOptions)!, eventDateUtc);
                break;
            case UploadEventType.Completed:
                Apply(JsonSerializer.Deserialize<UploadCompletedEvent>(json, JsonOptions)!, eventDateUtc);
                break;
            case UploadEventType.Validated:
                Apply(JsonSerializer.Deserialize<UploadValidatedEvent>(json, JsonOptions)!, eventDateUtc);
                break;
            case UploadEventType.Rejected:
                Apply(JsonSerializer.Deserialize<UploadRejectedEvent>(json, JsonOptions)!, eventDateUtc);
                break;
            case UploadEventType.Stored:
                Apply(JsonSerializer.Deserialize<UploadStoredEvent>(json, JsonOptions)!, eventDateUtc);
                break;
            case UploadEventType.Deleted:
                Apply(JsonSerializer.Deserialize<UploadDeletedEvent>(json, JsonOptions)!, eventDateUtc);
                break;
            default:
                throw new InvalidOperationException($"Unknown upload event type '{eventType}'.");
        }
    }

    public Guid Id { get; private set; }
    public DateTime DateCreatedUtc { get; private set; }
    public DateTime DateUpdatedUtc { get; private set; }
    public string UploadedByUserId { get; private set; } = string.Empty;
    public UploadState State { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public bool IsValidated { get; private set; }
    public bool IsRejected { get; private set; }
    public string StorageObjectKey { get; private set; } = string.Empty;
    public string? VerifiedStorageObjectKey { get; private set; }
    public string? Sha256Checksum { get; private set; }
    public long? FileSizeBytes { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;

    public static UploadAggregate FromEvents(IEnumerable<UploadEventEntity> events)
    {
        var aggregate = new UploadAggregate();
        foreach (var uploadEvent in events.OrderBy(e => e.EventDateUtc).ThenBy(e => e.Id))
        {
            aggregate.ApplyEvent(uploadEvent.EventType, uploadEvent.Json, uploadEvent.EventDateUtc);
        }

        return aggregate;
    }

    public string ResolveDownloadObjectKey()
    {
        if (State == UploadState.Stored && !string.IsNullOrWhiteSpace(VerifiedStorageObjectKey))
        {
            return VerifiedStorageObjectKey;
        }

        return StorageObjectKey;
    }

    public UploadDetailsInternalModel ToInternalDetailsModel()
    {
        return new UploadDetailsInternalModel
        {
            Id = Id,
            DateCreatedUtc = DateCreatedUtc,
            UploadedByUserId = UploadedByUserId,
            UploadedByUsername = UploadedByUsername,
            UploaderIpAddress = UploaderIpAddress,
            FileName = FileName,
            DateCompletedUtc = DateCompletedUtc,
            StorageObjectKey = string.IsNullOrWhiteSpace(StorageObjectKey) ? null : StorageObjectKey,
            Sha256Checksum = Sha256Checksum,
            DateValidatedUtc = DateValidatedUtc,
            DateRejectedUtc = DateRejectedUtc,
            DateStoredUtc = DateStoredUtc,
            VerifiedStorageObjectKey = VerifiedStorageObjectKey,
            DateDeletedUtc = DateDeletedUtc
        };
    }

    public UploadSummaryModel ToSummaryModel()
    {
        return new UploadSummaryModel
        {
            Id = Id,
            DateCreatedUtc = DateCreatedUtc,
            DateUpdatedUtc = DateUpdatedUtc,
            UploadedByUserId = UploadedByUserId,
            State = State,
            FileName = FileName,
            IsValidated = IsValidated,
            IsRejected = IsRejected,
            Sha256Checksum = Sha256Checksum,
            FileSizeBytes = FileSizeBytes
        };
    }

    public bool UsesVerifiedBucket()
    {
        return State == UploadState.Stored;
    }
}