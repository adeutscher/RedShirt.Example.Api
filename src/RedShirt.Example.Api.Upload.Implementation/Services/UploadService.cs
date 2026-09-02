using Microsoft.Extensions.Options;
using RedShirt.Example.Api.Common.Exceptions.Responses;
using RedShirt.Example.Api.Common.FileStorage.Services;
using RedShirt.Example.Api.Upload.Core.Configuration;
using RedShirt.Example.Api.Upload.Core.Events;
using RedShirt.Example.Api.Upload.Core.Models;
using RedShirt.Example.Api.Upload.Core.Services;
using RedShirt.Example.Api.Upload.Implementation.Repositories;

namespace RedShirt.Example.Api.Upload.Implementation.Services;

internal sealed class UploadService(
    IUploadRepository repository,
    IFileStorageService fileStorageService,
    IUploadEventBroadcaster eventBroadcaster,
    IOptions<UploadOptions> uploadOptions) : IUploadService
{
    public async Task<UploadSummaryModel> CreateAsync(UploadServiceCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FileName))
        {
            throw new BadRequestException("File name cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(request.UploadedByUserId))
        {
            throw new BadRequestException("Uploading user id cannot be empty.");
        }

        var uploadId = Guid.NewGuid();
        var createdEvent = new UploadCreatedEvent
        {
            UploadId = uploadId,
            UploadedByUserId = request.UploadedByUserId,
            UploadedByUsername = request.UploadedByUsername,
            UploaderIpAddress = request.UploaderIpAddress,
            FileName = request.FileName
        };

        var uploadingSummary = await repository.AppendEventAsync(uploadId, UploadEventTypes.Created, createdEvent,
            cancellationToken);
        await eventBroadcaster.BroadcastUploadCreatedAsync(createdEvent, uploadingSummary, cancellationToken);

        var options = uploadOptions.Value;
        var objectKey = BuildObjectKey(uploadId, request.FileName);
        var uploadResult = await fileStorageService.UploadAsync(options.BucketUnverifiedItems, objectKey,
            request.Content, cancellationToken);

        var completedEvent = new UploadCompletedEvent
        {
            UploadId = uploadId,
            StorageObjectKey = objectKey,
            Sha256Checksum = uploadResult.Sha256Checksum
        };

        var summary = await repository.AppendEventAsync(uploadId, UploadEventTypes.Completed, completedEvent,
            cancellationToken);
        await eventBroadcaster.BroadcastUploadCompletedAsync(completedEvent, summary, cancellationToken);
        return summary;
    }

    public async Task<UploadSummaryModel> GetSummaryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // ReSharper disable once ConvertIfStatementToReturnStatement
        if (await repository.GetSummaryAsync(id, cancellationToken) is not { } summary)
        {
            throw new ResourceNotFoundException();
        }

        return summary;
    }

    public async Task<UploadDetailsModel> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (await repository.GetSummaryAsync(id, cancellationToken) is null)
        {
            throw new ResourceNotFoundException();
        }

        var events = await repository.GetEventsAsync(id, cancellationToken);
        return new UploadDetailsModel
        {
            Id = id,
            Events = events.Select(e => new UploadEventRecordModel
            {
                Id = e.Id,
                EventDateUtc = e.EventDateUtc,
                EventType = e.EventType,
                Json = e.Json
            }).ToList()
        };
    }

    public Task<UploadSearchResponse> SearchAsync(UploadServiceSearchRequest parameters, Guid? continuationToken,
        CancellationToken cancellationToken = default)
    {
        return repository.SearchAsync(parameters, continuationToken, cancellationToken);
    }

    public async Task<UploadSummaryModel> SubmitVerdictAsync(UploadServiceVerdictRequest request,
        CancellationToken cancellationToken = default)
    {
        var aggregate = await repository.GetAggregateFromEventsAsync(request.UploadId, cancellationToken);
        if (aggregate.Id == Guid.Empty)
        {
            throw new ResourceNotFoundException();
        }

        if (aggregate.State != UploadState.NotValidated)
        {
            throw new BadRequestException("Upload is not awaiting validation.");
        }

        if (request.Approved)
        {
            var validatedEvent = new UploadValidatedEvent {UploadId = request.UploadId};
            var summary = await repository.AppendEventAsync(request.UploadId, UploadEventTypes.Validated, validatedEvent,
                cancellationToken);
            await eventBroadcaster.BroadcastUploadValidatedAsync(validatedEvent, summary, cancellationToken);
            return summary;
        }

        var rejectedEvent = new UploadRejectedEvent {UploadId = request.UploadId};
        var rejectedSummary = await repository.AppendEventAsync(request.UploadId, UploadEventTypes.Rejected,
            rejectedEvent, cancellationToken);
        await eventBroadcaster.BroadcastUploadRejectedAsync(rejectedEvent, rejectedSummary, cancellationToken);
        return rejectedSummary;
    }

    public async Task<UploadSummaryModel> SubmitMoveReportAsync(UploadServiceMoveReportRequest request,
        CancellationToken cancellationToken = default)
    {
        var aggregate = await repository.GetAggregateFromEventsAsync(request.UploadId, cancellationToken);
        if (aggregate.Id == Guid.Empty)
        {
            throw new ResourceNotFoundException();
        }

        if (aggregate.State != UploadState.Verified)
        {
            throw new BadRequestException("Upload is not awaiting a move report.");
        }

        if (string.IsNullOrWhiteSpace(request.VerifiedStorageObjectKey))
        {
            throw new BadRequestException("Verified storage object key cannot be empty.");
        }

        var storedEvent = new UploadStoredEvent
        {
            UploadId = request.UploadId,
            VerifiedStorageObjectKey = request.VerifiedStorageObjectKey
        };

        var summary = await repository.AppendEventAsync(request.UploadId, UploadEventTypes.Stored, storedEvent,
            cancellationToken);
        await eventBroadcaster.BroadcastUploadStoredAsync(storedEvent, summary, cancellationToken);
        return summary;
    }

    public async Task<UploadSummaryModel> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var aggregate = await repository.GetAggregateFromEventsAsync(id, cancellationToken);
        if (aggregate.Id == Guid.Empty)
        {
            throw new ResourceNotFoundException();
        }

        if (aggregate.State == UploadState.Deleted)
        {
            throw new BadRequestException("Upload is already deleted.");
        }

        var options = uploadOptions.Value;
        var bucket = aggregate.UsesVerifiedBucket()
            ? options.BucketVerifiedItems
            : options.BucketUnverifiedItems;
        var objectKey = aggregate.ResolveDownloadObjectKey();

        if (!string.IsNullOrWhiteSpace(objectKey))
        {
            await fileStorageService.DeleteAsync(bucket, objectKey, cancellationToken);
        }

        var deletedEvent = new UploadDeletedEvent {UploadId = id};
        var summary = await repository.AppendEventAsync(id, UploadEventTypes.Deleted, deletedEvent,
            cancellationToken);
        await eventBroadcaster.BroadcastUploadDeletedAsync(deletedEvent, summary, cancellationToken);
        return summary;
    }

    public async Task<UploadDownloadLinkModel> GetDownloadLinkAsync(Guid id,
        CancellationToken cancellationToken = default)
    {
        var aggregate = await repository.GetAggregateFromEventsAsync(id, cancellationToken);
        if (aggregate.Id == Guid.Empty)
        {
            throw new ResourceNotFoundException();
        }

        if (aggregate.State is UploadState.Uploading or UploadState.Deleted)
        {
            throw new BadRequestException("Upload is not available for download.");
        }

        var options = uploadOptions.Value;
        var bucket = aggregate.UsesVerifiedBucket()
            ? options.BucketVerifiedItems
            : options.BucketUnverifiedItems;
        var objectKey = aggregate.ResolveDownloadObjectKey();
        var validity = TimeSpan.FromMinutes(options.PresignedUrlLifetimeMinutes);
        var url = await fileStorageService.GetPresignedDownloadUrlAsync(bucket, objectKey, validity,
            cancellationToken);

        return new UploadDownloadLinkModel
        {
            DownloadUrl = url,
            ExpiresAtUtc = DateTime.UtcNow.Add(validity)
        };
    }

    private static string BuildObjectKey(Guid uploadId, string fileName)
    {
        return $"{uploadId:N}/{fileName}";
    }
}
