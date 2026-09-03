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

    public Guid Id { get; private set; }
    public DateTime DateCreatedUtc { get; private set; }
    public DateTime DateUpdatedUtc { get; private set; }
    public string UploadedByUserId { get; private set; } = string.Empty;
    public string UploadedByUsername { get; private set; } = string.Empty;
    public string UploaderIpAddress { get; private set; } = string.Empty;
    public UploadState State { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public bool IsValidated { get; private set; }
    public bool IsRejected { get; private set; }
    public string StorageObjectKey { get; private set; } = string.Empty;
    public string? VerifiedStorageObjectKey { get; private set; }
    public string? Sha256Checksum { get; private set; }
    public DateTime? DateCompletedUtc { get; private set; }
    public DateTime? DateValidatedUtc { get; private set; }
    public DateTime? DateRejectedUtc { get; private set; }
    public DateTime? DateStoredUtc { get; private set; }
    public DateTime? DateDeletedUtc { get; private set; }
    public string IdempotencyKey { get; private set; } = string.Empty;

    public void Apply(UploadCreatedEvent uploadEvent, DateTime eventDateUtc)
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

    public void Apply(UploadCompletedEvent uploadEvent, DateTime eventDateUtc)
    {
        StorageObjectKey = uploadEvent.StorageObjectKey;
        Sha256Checksum = uploadEvent.Sha256Checksum;
        DateCompletedUtc = eventDateUtc;
        DateUpdatedUtc = eventDateUtc;
        State = UploadState.NotValidated;
    }

    public void Apply(UploadValidatedEvent uploadEvent, DateTime eventDateUtc)
    {
        IsValidated = true;
        DateValidatedUtc = eventDateUtc;
        DateUpdatedUtc = eventDateUtc;
        State = UploadState.Verified;
    }

    public void Apply(UploadRejectedEvent uploadEvent, DateTime eventDateUtc)
    {
        IsRejected = true;
        DateRejectedUtc = eventDateUtc;
        DateUpdatedUtc = eventDateUtc;
        State = UploadState.Rejected;
    }

    public void Apply(UploadStoredEvent uploadEvent, DateTime eventDateUtc)
    {
        VerifiedStorageObjectKey = uploadEvent.VerifiedStorageObjectKey;
        DateStoredUtc = eventDateUtc;
        DateUpdatedUtc = eventDateUtc;
        State = UploadState.Stored;
    }

    public void Apply(UploadDeletedEvent uploadEvent, DateTime eventDateUtc)
    {
        DateDeletedUtc = eventDateUtc;
        DateUpdatedUtc = eventDateUtc;
        State = UploadState.Deleted;
    }

    public void ApplyEvent(UploadEventType eventType, string json, DateTime eventDateUtc)
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
            IsRejected = IsRejected
        };
    }

    public bool UsesVerifiedBucket()
    {
        return State == UploadState.Stored;
    }
}