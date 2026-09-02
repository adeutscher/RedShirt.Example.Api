using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RedShirt.Example.Api.Common.Distributed.Extensions;
using RedShirt.Example.Api.Common.Distributed.Services.Abstractions;
using RedShirt.Example.Api.DataStores.Constants;
using RedShirt.Example.Api.Upload.Core.Models;
using RedShirt.Example.Api.Upload.Core.Services;
using RedShirt.Example.Api.Upload.Implementation.Aggregates;
using RedShirt.Example.Api.Upload.Implementation.Entities;
using RedShirt.Example.Api.Upload.Implementation.Factories;
using RedShirt.Example.Api.Upload.Implementation.Predicates;

namespace RedShirt.Example.Api.Upload.Implementation.Repositories;

internal interface IUploadRepository
{
    Task<UploadSummaryModel> AppendEventAsync(Guid uploadId, string eventType, object eventPayload,
        CancellationToken cancellationToken = default);

    Task<UploadSummaryModel?> GetSummaryAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UploadEventEntity>> GetEventsAsync(Guid uploadId,
        CancellationToken cancellationToken = default);

    Task<UploadSearchResponse> SearchAsync(UploadServiceSearchRequest parameters, Guid? continuationToken,
        CancellationToken cancellationToken = default);

    Task<UploadAggregate> GetAggregateFromEventsAsync(Guid uploadId,
        CancellationToken cancellationToken = default);
}

internal sealed class UploadRepository(
    IUploadDbContextFactory dbContextFactory,
    IRemoteCacheService cacheService) : IUploadRepository
{
    private const int MaxPageSize = 100;
    private const string ConnectionStringName = DatabaseConstants.PrimaryDatabaseConnectionStringName;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static UploadSummaryModel ToSummary(UploadAggregateEntity entity)
    {
        return new UploadSummaryModel
        {
            Id = entity.Id,
            DateCreatedUtc = entity.DateCreatedUtc,
            DateUpdatedUtc = entity.DateUpdatedUtc,
            UploadedByUserId = entity.UploadedByUserId,
            State = Enum.Parse<UploadState>(entity.State),
            FileName = entity.FileName,
            IsValidated = entity.IsValidated,
            IsRejected = entity.IsRejected
        };
    }

    private static PredicateBuilder<UploadAggregateEntity> BuildSearchPredicate(UploadServiceSearchRequest parameters)
    {
        var builder = new PredicateBuilder<UploadAggregateEntity>();

        if (parameters.Id.HasValue)
        {
            var id = parameters.Id.Value;
            builder.And(e => e.Id == id);
        }

        if (parameters.CreatedBeforeUtc.HasValue)
        {
            var createdBefore = parameters.CreatedBeforeUtc.Value;
            builder.And(e => e.DateCreatedUtc < createdBefore);
        }

        if (parameters.CreatedAfterUtc.HasValue)
        {
            var createdAfter = parameters.CreatedAfterUtc.Value;
            builder.And(e => e.DateCreatedUtc > createdAfter);
        }

        if (parameters.UpdatedBeforeUtc.HasValue)
        {
            var updatedBefore = parameters.UpdatedBeforeUtc.Value;
            builder.And(e => e.DateUpdatedUtc < updatedBefore);
        }

        if (parameters.UpdatedAfterUtc.HasValue)
        {
            var updatedAfter = parameters.UpdatedAfterUtc.Value;
            builder.And(e => e.DateUpdatedUtc > updatedAfter);
        }

        if (parameters.State.HasValue)
        {
            var state = parameters.State.Value.ToString();
            builder.And(e => e.State == state);
        }

        if (!string.IsNullOrWhiteSpace(parameters.UploadedByUserId))
        {
            var userId = parameters.UploadedByUserId;
            builder.And(e => e.UploadedByUserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(parameters.FileName))
        {
            var fileName = parameters.FileName;
            builder.And(e => e.FileName == fileName);
        }

        if (parameters.IsValidated.HasValue)
        {
            var isValidated = parameters.IsValidated.Value;
            builder.And(e => e.IsValidated == isValidated);
        }

        // ReSharper disable once InvertIf
        if (parameters.IsRejected.HasValue)
        {
            var isRejected = parameters.IsRejected.Value;
            builder.And(e => e.IsRejected == isRejected);
        }

        return builder;
    }

    public async Task<UploadSummaryModel> AppendEventAsync(Guid uploadId, string eventType, object eventPayload,
        CancellationToken cancellationToken = default)
    {
        var eventDateUtc = DateTime.UtcNow;
        var json = JsonSerializer.Serialize(eventPayload, JsonOptions);

        await using var context = await dbContextFactory.CreateDbContextAsync(ConnectionStringName, cancellationToken);

        context.UploadEvents.Add(new UploadEventEntity
        {
            Id = Guid.NewGuid(),
            UploadId = uploadId,
            EventDateUtc = eventDateUtc,
            EventType = eventType,
            Json = json
        });

        var events = await context.UploadEvents.AsNoTracking()
            .Where(e => e.UploadId == uploadId)
            .OrderBy(e => e.EventDateUtc)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);

        // Include the event we just staged (not yet committed) for aggregate projection.
        events.Add(new UploadEventEntity
        {
            Id = Guid.Empty,
            UploadId = uploadId,
            EventDateUtc = eventDateUtc,
            EventType = eventType,
            Json = json
        });

        var aggregate = UploadAggregate.FromEvents(events);
        var entity = aggregate.ToEntity();

        var existing = await context.UploadAggregates.FirstOrDefaultAsync(e => e.Id == uploadId, cancellationToken);
        if (existing is null)
        {
            context.UploadAggregates.Add(entity);
        }
        else
        {
            existing.DateCreatedUtc = entity.DateCreatedUtc;
            existing.DateUpdatedUtc = entity.DateUpdatedUtc;
            existing.UploadedByUserId = entity.UploadedByUserId;
            existing.State = entity.State;
            existing.FileName = entity.FileName;
            existing.IsValidated = entity.IsValidated;
            existing.IsRejected = entity.IsRejected;
        }

        await context.SaveChangesAsync(cancellationToken);
        return aggregate.ToSummaryModel();
    }

    public async Task<UploadSummaryModel?> GetSummaryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ConnectionStringName, cancellationToken);
        var entity = await context.UploadAggregates.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        return entity is null ? null : ToSummary(entity);
    }

    public async Task<IReadOnlyList<UploadEventEntity>> GetEventsAsync(Guid uploadId,
        CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ConnectionStringName, cancellationToken);
        return await context.UploadEvents.AsNoTracking()
            .Where(e => e.UploadId == uploadId)
            .OrderBy(e => e.EventDateUtc)
            .ThenBy(e => e.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<UploadSearchResponse> SearchAsync(UploadServiceSearchRequest parameters,
        Guid? continuationToken, CancellationToken cancellationToken = default)
    {
        ContinuationParameters? continuationParameters = null;
        if (continuationToken is not null)
        {
            continuationParameters =
                await cacheService.GetObjectAsync<ContinuationParameters>($"continuation:{continuationToken.Value}",
                    cancellationToken);
        }

        if (continuationParameters is not null)
        {
            parameters = continuationParameters.SearchParameters;
        }

        await using var context = await dbContextFactory.CreateDbContextAsync(ConnectionStringName, cancellationToken);

        var predicate = BuildSearchPredicate(parameters);
        if (continuationParameters is not null)
        {
            var checkpoint = continuationParameters.LastUpdatedAtUtc;
            var lastId = continuationParameters.LastId;
            predicate.And(e => e.DateUpdatedUtc <= checkpoint && e.Id != lastId);
        }

        var pageSize = parameters.PageSize <= 0 ? MaxPageSize : Math.Min(MaxPageSize, parameters.PageSize);

        var entities = await context.UploadAggregates.AsNoTracking()
            .Where(predicate.Build())
            .OrderByDescending(e => e.DateUpdatedUtc)
            .ThenByDescending(e => e.Id)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var records = entities.Select(ToSummary).ToList();
        continuationToken = records.Count >= pageSize ? Guid.NewGuid() : null;

        if (continuationToken.HasValue)
        {
            var lastRecord = records[^1];
            await cacheService.SetObjectAsync(
                $"continuation:{continuationToken.Value}",
                new ContinuationParameters
                {
                    SearchParameters = parameters,
                    LastId = lastRecord.Id,
                    LastUpdatedAtUtc = lastRecord.DateUpdatedUtc
                },
                TimeSpan.FromMinutes(5),
                cancellationToken);
        }

        return new UploadSearchResponse
        {
            Records = records,
            ContinuationToken = continuationToken
        };
    }

    public async Task<UploadAggregate> GetAggregateFromEventsAsync(Guid uploadId,
        CancellationToken cancellationToken = default)
    {
        var events = await GetEventsAsync(uploadId, cancellationToken);
        return UploadAggregate.FromEvents(events);
    }

    private sealed class ContinuationParameters
    {
        public required UploadServiceSearchRequest SearchParameters { get; init; }
        public required Guid LastId { get; init; }
        public required DateTime LastUpdatedAtUtc { get; init; }
    }
}
