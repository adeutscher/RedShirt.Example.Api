using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RedShirt.Example.Api.BackgroundJobs.Core.Configuration;
using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Core.Services;
using RedShirt.Example.Api.BackgroundJobs.Implementation.Data;
using RedShirt.Example.Api.BackgroundJobs.Implementation.Entities;
using RedShirt.Example.Api.BackgroundJobs.Implementation.Factories;
using RedShirt.Example.Api.Common.Exceptions.Responses;
using RedShirt.Example.Api.DataStores.Constants;

namespace RedShirt.Example.Api.BackgroundJobs.Implementation.Services;

internal sealed class BackgroundJobService(
    IBackgroundJobDbContextFactory dbContextFactory,
    IBackgroundJobWakeSignal wakeSignal,
    TimeProvider timeProvider,
    IOptions<BackgroundJobOptions> options) : IBackgroundJobService
{
    private const int MaxPageSize = 100;
    private const string ConnectionStringName = DatabaseConstants.PrimaryDatabaseConnectionStringName;

    private DateTime UtcNow => timeProvider.GetUtcNow().UtcDateTime;

    private static string? TrimDetail(string? detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
        {
            return null;
        }

        return detail.Length <= BackgroundJobLimits.DetailMaxLength
            ? detail
            : detail[..BackgroundJobLimits.DetailMaxLength];
    }

    private static BackgroundJobModel ToModel(BackgroundJobEntity entity)
    {
        return new BackgroundJobModel
        {
            Id = entity.Id,
            Type = entity.Type,
            State = entity.State,
            ParametersJson = entity.ParametersJson,
            Attempts = entity.Attempts,
            DateCreatedUtc = entity.DateCreatedUtc,
            DateUpdatedUtc = entity.DateUpdatedUtc,
            DueAtUtc = entity.DueAtUtc,
            LeaseOwner = entity.LeaseOwner,
            LeaseExpiresAtUtc = entity.LeaseExpiresAtUtc,
            CompletedAtUtc = entity.CompletedAtUtc,
            Detail = entity.Detail
        };
    }

    private static async Task<BackgroundJobModel> GetRequiredAsync(BackgroundJobDbContext context, Guid id,
        CancellationToken cancellationToken)
    {
        var entity = await context.BackgroundJobs.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        return entity is null ? throw new ResourceNotFoundException() : ToModel(entity);
    }

    public async Task<BackgroundJobModel> CreateAsync(BackgroundJobServiceCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.Type))
        {
            throw new BadRequestException("Unknown background job type.");
        }

        if (string.IsNullOrWhiteSpace(request.ParametersJson))
        {
            throw new BadRequestException("Background job parameters cannot be empty.");
        }

        var now = UtcNow;
        var entity = new BackgroundJobEntity
        {
            Id = Guid.NewGuid(),
            DateCreatedUtc = now,
            DateUpdatedUtc = now,
            Type = request.Type,
            State = BackgroundJobState.Pending,
            ParametersJson = request.ParametersJson,
            Attempts = 0,
            DueAtUtc = now
        };

        await using (var context =
                     await dbContextFactory.CreateDbContextAsync(ConnectionStringName, cancellationToken))
        {
            context.BackgroundJobs.Add(entity);
            await context.SaveChangesAsync(cancellationToken);
        }

        wakeSignal.Wake();
        return ToModel(entity);
    }

    public async Task<BackgroundJobModel> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ConnectionStringName, cancellationToken);
        return await GetRequiredAsync(context, id, cancellationToken);
    }

    public async Task<BackgroundJobSearchResponse> SearchAsync(BackgroundJobServiceSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ConnectionStringName, cancellationToken);

        var query = context.BackgroundJobs.AsNoTracking();

        if (request.Type.HasValue)
        {
            var type = request.Type.Value;
            query = query.Where(e => e.Type == type);
        }

        if (request.State.HasValue)
        {
            var state = request.State.Value;
            query = query.Where(e => e.State == state);
        }

        if (request.CreatedBeforeUtc.HasValue)
        {
            var createdBefore = request.CreatedBeforeUtc.Value;
            query = query.Where(e => e.DateCreatedUtc < createdBefore);
        }

        var pageSize = request.PageSize <= 0 ? MaxPageSize : Math.Min(MaxPageSize, request.PageSize);
        var entities = await query
            .OrderByDescending(e => e.DateCreatedUtc)
            .ThenByDescending(e => e.Id)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new BackgroundJobSearchResponse
        {
            Records = entities.Select(ToModel).ToList()
        };
    }

    public async Task<IReadOnlyList<BackgroundJobModel>> LeaseAsync(BackgroundJobServiceLeaseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.LeaseOwner))
        {
            throw new BadRequestException("Lease owner cannot be empty.");
        }

        if (request.LeaseOwner.Length > BackgroundJobLimits.LeaseOwnerMaxLength)
        {
            throw new BadRequestException(
                $"Lease owner cannot be longer than {BackgroundJobLimits.LeaseOwnerMaxLength} characters.");
        }

        if (request.Count <= 0)
        {
            return [];
        }

        var settings = options.Value;
        var maxAttempts = settings.EffectiveMaxAttempts;
        var now = UtcNow;
        var leaseExpiresAtUtc = now.AddSeconds(settings.EffectiveLeaseSeconds);

        await using var context = await dbContextFactory.CreateDbContextAsync(ConnectionStringName, cancellationToken);

        var candidates = await context.BackgroundJobs.AsNoTracking()
            .Where(e => (e.State == BackgroundJobState.Pending && e.DueAtUtc <= now)
                        || (e.State == BackgroundJobState.Running && e.LeaseExpiresAtUtc < now))
            .OrderBy(e => e.DueAtUtc)
            .ThenBy(e => e.Id)
            .Take(request.Count)
            .Select(e => new {e.Id, e.State, e.Attempts})
            .ToListAsync(cancellationToken);

        var leasedIds = new List<Guid>();
        foreach (var candidate in candidates)
        {
            /*
             * Each claim is a conditional update on the state and attempt count that were just read. If another process
             * claimed the row in the meantime, nothing matches and the row is skipped. This keeps leasing safe without
             * database-specific locking hints (such as FOR UPDATE SKIP LOCKED), so the unit tests can run on SQLite.
             */
            var claim = context.BackgroundJobs
                .Where(e => e.Id == candidate.Id
                            && e.State == candidate.State
                            && e.Attempts == candidate.Attempts);

            if (candidate.State == BackgroundJobState.Running && candidate.Attempts >= maxAttempts)
            {
                // The process holding the final attempt never acknowledged it (for example, it crashed mid-job).
                await claim.ExecuteUpdateAsync(setters => setters
                        .SetProperty(e => e.State, BackgroundJobState.Failed)
                        .SetProperty(e => e.CompletedAtUtc, now)
                        .SetProperty(e => e.DateUpdatedUtc, now)
                        .SetProperty(e => e.LeaseOwner, (string?) null)
                        .SetProperty(e => e.LeaseExpiresAtUtc, (DateTime?) null)
                        .SetProperty(e => e.Detail, "The lease on the final attempt expired without an outcome."),
                    cancellationToken);
                continue;
            }

            var claimed = await claim.ExecuteUpdateAsync(setters => setters
                    .SetProperty(e => e.State, BackgroundJobState.Running)
                    .SetProperty(e => e.Attempts, candidate.Attempts + 1)
                    .SetProperty(e => e.LeaseOwner, request.LeaseOwner)
                    .SetProperty(e => e.LeaseExpiresAtUtc, leaseExpiresAtUtc)
                    .SetProperty(e => e.DateUpdatedUtc, now),
                cancellationToken);

            if (claimed == 1)
            {
                leasedIds.Add(candidate.Id);
            }
        }

        if (leasedIds.Count == 0)
        {
            return [];
        }

        var leased = await context.BackgroundJobs.AsNoTracking()
            .Where(e => leasedIds.Contains(e.Id))
            .ToListAsync(cancellationToken);

        return leased
            .OrderBy(e => leasedIds.IndexOf(e.Id))
            .Select(ToModel)
            .ToList();
    }

    public async Task<BackgroundJobModel> AcknowledgeAsync(BackgroundJobServiceAcknowledgeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(request.Result))
        {
            throw new BadRequestException("Unknown job result.");
        }

        var settings = options.Value;
        var now = UtcNow;
        var detail = TrimDetail(request.Detail);

        await using var context = await dbContextFactory.CreateDbContextAsync(ConnectionStringName, cancellationToken);

        var existing = await GetRequiredAsync(context, request.Id, cancellationToken);
        if (existing.State != BackgroundJobState.Running
            || !string.Equals(existing.LeaseOwner, request.LeaseOwner, StringComparison.Ordinal))
        {
            throw new ConflictException("Background job is not leased by this lease owner.");
        }

        var nextState = request.Result switch
        {
            JobResult.Success => BackgroundJobState.Succeeded,
            JobResult.Failure when existing.Attempts < settings.EffectiveMaxAttempts => BackgroundJobState.Pending,
            _ => BackgroundJobState.Failed
        };

        var completedAtUtc = nextState == BackgroundJobState.Pending ? (DateTime?) null : now;
        var dueAtUtc = nextState == BackgroundJobState.Pending
            ? now.Add(settings.GetRetryDelay(existing.Attempts))
            : existing.DueAtUtc;

        // Conditional on the lease that was just checked, in case the lease expired and was taken in the meantime.
        var updated = await context.BackgroundJobs
            .Where(e => e.Id == request.Id
                        && e.State == BackgroundJobState.Running
                        && e.LeaseOwner == request.LeaseOwner
                        && e.Attempts == existing.Attempts)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(e => e.State, nextState)
                    .SetProperty(e => e.DueAtUtc, dueAtUtc)
                    .SetProperty(e => e.CompletedAtUtc, completedAtUtc)
                    .SetProperty(e => e.LeaseOwner, (string?) null)
                    .SetProperty(e => e.LeaseExpiresAtUtc, (DateTime?) null)
                    .SetProperty(e => e.Detail, detail)
                    .SetProperty(e => e.DateUpdatedUtc, now),
                cancellationToken);

        if (updated != 1)
        {
            throw new ConflictException("Background job is not leased by this lease owner.");
        }

        return await GetRequiredAsync(context, request.Id, cancellationToken);
    }

    public async Task<BackgroundJobModel> RetryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var now = UtcNow;

        await using (var context =
                     await dbContextFactory.CreateDbContextAsync(ConnectionStringName, cancellationToken))
        {
            var existing = await GetRequiredAsync(context, id, cancellationToken);
            if (existing.State != BackgroundJobState.Failed)
            {
                throw new BadRequestException("Only failed background jobs can be retried.");
            }

            var updated = await context.BackgroundJobs
                .Where(e => e.Id == id && e.State == BackgroundJobState.Failed)
                .ExecuteUpdateAsync(setters => setters
                        .SetProperty(e => e.State, BackgroundJobState.Pending)
                        .SetProperty(e => e.Attempts, 0)
                        .SetProperty(e => e.DueAtUtc, now)
                        .SetProperty(e => e.CompletedAtUtc, (DateTime?) null)
                        .SetProperty(e => e.DateUpdatedUtc, now),
                    cancellationToken);

            if (updated != 1)
            {
                throw new ConflictException("Background job changed while it was being retried.");
            }
        }

        wakeSignal.Wake();
        return await GetAsync(id, cancellationToken);
    }
}
