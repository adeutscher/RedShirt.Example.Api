using System.Linq.Expressions;
using System.Text.Json;
using RedShirt.Example.Api.Upload.Core.Events;
using RedShirt.Example.Api.Upload.Core.Models;
using RedShirt.Example.Api.Upload.Implementation.Entities;

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
    public UploadState State { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public bool IsValidated { get; private set; }
    public bool IsRejected { get; private set; }
    public string StorageObjectKey { get; private set; } = string.Empty;
    public string? VerifiedStorageObjectKey { get; private set; }

    public static UploadAggregate FromEvents(IEnumerable<UploadEventEntity> events)
    {
        var aggregate = new UploadAggregate();
        foreach (var uploadEvent in events.OrderBy(e => e.EventDateUtc).ThenBy(e => e.Id))
        {
            aggregate.ApplyEvent(uploadEvent.EventType, uploadEvent.Json, uploadEvent.EventDateUtc);
        }

        return aggregate;
    }

    public void ApplyEvent(string eventType, string json, DateTime eventDateUtc)
    {
        switch (eventType)
        {
            case UploadEventTypes.Created:
                Apply(JsonSerializer.Deserialize<UploadCreatedEvent>(json, JsonOptions)!, eventDateUtc);
                break;
            case UploadEventTypes.Completed:
                Apply(JsonSerializer.Deserialize<UploadCompletedEvent>(json, JsonOptions)!, eventDateUtc);
                break;
            case UploadEventTypes.Validated:
                Apply(JsonSerializer.Deserialize<UploadValidatedEvent>(json, JsonOptions)!, eventDateUtc);
                break;
            case UploadEventTypes.Rejected:
                Apply(JsonSerializer.Deserialize<UploadRejectedEvent>(json, JsonOptions)!, eventDateUtc);
                break;
            case UploadEventTypes.Stored:
                Apply(JsonSerializer.Deserialize<UploadStoredEvent>(json, JsonOptions)!, eventDateUtc);
                break;
            case UploadEventTypes.Deleted:
                Apply(JsonSerializer.Deserialize<UploadDeletedEvent>(json, JsonOptions)!, eventDateUtc);
                break;
            default:
                throw new InvalidOperationException($"Unknown upload event type '{eventType}'.");
        }
    }

    public void Apply(UploadCreatedEvent uploadEvent, DateTime eventDateUtc)
    {
        Id = uploadEvent.UploadId;
        UploadedByUserId = uploadEvent.UploadedByUserId;
        FileName = uploadEvent.FileName;
        DateCreatedUtc = eventDateUtc;
        DateUpdatedUtc = eventDateUtc;
        State = UploadState.Uploading;
    }

    public void Apply(UploadCompletedEvent uploadEvent, DateTime eventDateUtc)
    {
        StorageObjectKey = uploadEvent.StorageObjectKey;
        DateUpdatedUtc = eventDateUtc;
        State = UploadState.NotValidated;
    }

    public void Apply(UploadValidatedEvent uploadEvent, DateTime eventDateUtc)
    {
        IsValidated = true;
        DateUpdatedUtc = eventDateUtc;
        State = UploadState.Verified;
    }

    public void Apply(UploadRejectedEvent uploadEvent, DateTime eventDateUtc)
    {
        IsRejected = true;
        DateUpdatedUtc = eventDateUtc;
        State = UploadState.Rejected;
    }

    public void Apply(UploadStoredEvent uploadEvent, DateTime eventDateUtc)
    {
        VerifiedStorageObjectKey = uploadEvent.VerifiedStorageObjectKey;
        DateUpdatedUtc = eventDateUtc;
        State = UploadState.Stored;
    }

    public void Apply(UploadDeletedEvent uploadEvent, DateTime eventDateUtc)
    {
        DateUpdatedUtc = eventDateUtc;
        State = UploadState.Deleted;
    }

    public UploadAggregateEntity ToEntity()
    {
        return new UploadAggregateEntity
        {
            Id = Id,
            DateCreatedUtc = DateCreatedUtc,
            DateUpdatedUtc = DateUpdatedUtc,
            UploadedByUserId = UploadedByUserId,
            State = State.ToString(),
            FileName = FileName,
            IsValidated = IsValidated,
            IsRejected = IsRejected
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

    public string ResolveDownloadObjectKey()
    {
        if (State == UploadState.Stored && !string.IsNullOrWhiteSpace(VerifiedStorageObjectKey))
        {
            return VerifiedStorageObjectKey;
        }

        return StorageObjectKey;
    }

    public bool UsesVerifiedBucket() => State == UploadState.Stored;
}
