using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RedShirt.Example.Api.Common.Exceptions.Responses;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Configuration;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Services;
using RedShirt.Example.Api.DataStores.BackgroundJob.Implementation.Data;
using RedShirt.Example.Api.DataStores.BackgroundJob.Implementation.Entities;
using RedShirt.Example.Api.DataStores.BackgroundJob.Implementation.Factories;
using RedShirt.Example.Api.DataStores.Constants;

namespace RedShirt.Example.Api.DataStores.BackgroundJob.Implementation.Services;

internal sealed class BackgroundJobService(
    IBackgroundJobDbContextFactory dbContextFactory,
    TimeProvider timeProvider,
    IOptions<BackgroundJobStoreOptions> options) : IBackgroundJobService
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

    /// <summary>
    ///     The job, if it is running at <paramref name="attempt" />. The attempt number fences a report to one lease:
    ///     every lease increments it, so a report from a lease that expired and was taken again matches nothing.
    /// </summary>
    private static IQueryable<BackgroundJobEntity> RunningAttempt(BackgroundJobDbContext context, Guid id, int attempt)
    {
        return context.BackgroundJobs
            .Where(e => e.Id == id && e.State == BackgroundJobState.Running && e.Attempts == attempt);
    }

    /// <summary>
    ///     Returns the job after a conditional status report, or explains why the report matched nothing.
    /// </summary>
    private static async Task<BackgroundJobModel> GetReportedAsync(BackgroundJobDbContext context, Guid id,
        int updated, string report, CancellationToken cancellationToken)
    {
        // Throws ResourceNotFoundException when the job does not exist.
        var model = await GetRequiredAsync(context, id, cancellationToken);
        return updated == 1
            ? model
            : throw new ConflictException(
                $"Background job cannot be {report}: it is not at that attempt, or not in a state for this report.");
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

        return ToModel(entity);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var context = await dbContextFactory.CreateDbContextAsync(ConnectionStringName, cancellationToken);

        var existing = await GetRequiredAsync(context, id, cancellationToken);
        if (existing.State is not (BackgroundJobState.Succeeded or BackgroundJobState.Failed))
        {
            throw new BadRequestException("Only finished background jobs can be deleted.");
        }

        // Conditional on the job still being finished, in case it was retried in the meantime.
        var deleted = await context.BackgroundJobs
            .Where(e => e.Id == id
                        && (e.State == BackgroundJobState.Succeeded || e.State == BackgroundJobState.Failed))
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted != 1)
        {
            throw new ConflictException("Background job changed while it was being deleted.");
        }
    }

    public async Task<BackgroundJobModel> FailAsync(BackgroundJobServiceFailRequest request,
        CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var now = UtcNow;
        var retry = request.CanRetry && request.Attempt < settings.EffectiveMaxAttempts;

        await using var context = await dbContextFactory.CreateDbContextAsync(ConnectionStringName, cancellationToken);

        var attempt = RunningAttempt(context, request.Id, request.Attempt);
        var detail = TrimDetail(request.Detail);
        int updated;
        if (retry)
        {
            var dueAtUtc = now.Add(settings.GetRetryDelay(request.Attempt));
            updated = await attempt.ExecuteUpdateAsync(setters => setters
                    .SetProperty(e => e.State, BackgroundJobState.Pending)
                    .SetProperty(e => e.DueAtUtc, dueAtUtc)
                    .SetProperty(e => e.CompletedAtUtc, (DateTime?) null)
                    .SetProperty(e => e.LeaseOwner, (string?) null)
                    .SetProperty(e => e.LeaseExpiresAtUtc, (DateTime?) null)
                    .SetProperty(e => e.Detail, detail)
                    .SetProperty(e => e.DateUpdatedUtc, now),
                cancellationToken);
        }
        else
        {
            updated = await attempt.ExecuteUpdateAsync(setters => setters
                    .SetProperty(e => e.State, BackgroundJobState.Failed)
                    .SetProperty(e => e.CompletedAtUtc, now)
                    .SetProperty(e => e.LeaseOwner, (string?) null)
                    .SetProperty(e => e.LeaseExpiresAtUtc, (DateTime?) null)
                    .SetProperty(e => e.Detail, detail)
                    .SetProperty(e => e.DateUpdatedUtc, now),
                cancellationToken);
        }

        return await GetReportedAsync(context, request.Id, updated, "failed", cancellationToken);
    }

    public async Task<BackgroundJobModel> FinishAsync(BackgroundJobServiceFinishRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = UtcNow;

        await using var context = await dbContextFactory.CreateDbContextAsync(ConnectionStringName, cancellationToken);

        var updated = await RunningAttempt(context, request.Id, request.Attempt)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(e => e.State, BackgroundJobState.Succeeded)
                    .SetProperty(e => e.CompletedAtUtc, now)
                    .SetProperty(e => e.LeaseOwner, (string?) null)
                    .SetProperty(e => e.LeaseExpiresAtUtc, (DateTime?) null)
                    .SetProperty(e => e.Detail, TrimDetail(request.Detail))
                    .SetProperty(e => e.DateUpdatedUtc, now),
                cancellationToken);

        return await GetReportedAsync(context, request.Id, updated, "finished", cancellationToken);
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

        if (request.CompletedBeforeUtc.HasValue)
        {
            // CompletedAtUtc is only set on finished jobs, so pending and running jobs never match.
            var completedBefore = request.CompletedBeforeUtc.Value;
            query = query.Where(e => e.CompletedAtUtc != null && e.CompletedAtUtc < completedBefore);
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

        // A leased job stays pending until the job logic reports its start, so a pending job can already be leased.
        var candidates = await context.BackgroundJobs.AsNoTracking()
            .Where(e => (e.State == BackgroundJobState.Pending
                         && e.DueAtUtc <= now
                         && (e.LeaseExpiresAtUtc == null || e.LeaseExpiresAtUtc < now))
                        || (e.State == BackgroundJobState.Running && e.LeaseExpiresAtUtc < now))
            .OrderBy(e => e.DueAtUtc)
            .ThenBy(e => e.Id)
            .Take(request.Count)
            .Select(e => new {e.Id, e.State, e.Attempts, e.Detail})
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

            if (candidate.Attempts >= maxAttempts)
            {
                // The final attempt was leased and never reported an outcome (for example, the process crashed).
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

            // A running job whose lease expired is made pending again: the new attempt has not started yet.
            var detail = candidate.State == BackgroundJobState.Running
                ? "The previous attempt's lease expired without an outcome."
                : candidate.Detail;

            // Not set to running here. The job logic reports the start (StartAsync) once it begins.
            var claimed = await claim.ExecuteUpdateAsync(setters => setters
                    .SetProperty(e => e.State, BackgroundJobState.Pending)
                    .SetProperty(e => e.Attempts, candidate.Attempts + 1)
                    .SetProperty(e => e.LeaseOwner, request.LeaseOwner)
                    .SetProperty(e => e.LeaseExpiresAtUtc, leaseExpiresAtUtc)
                    .SetProperty(e => e.Detail, detail)
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
                        .SetProperty(e => e.LeaseOwner, (string?) null)
                        .SetProperty(e => e.LeaseExpiresAtUtc, (DateTime?) null)
                        .SetProperty(e => e.DateUpdatedUtc, now),
                    cancellationToken);

            if (updated != 1)
            {
                throw new ConflictException("Background job changed while it was being retried.");
            }
        }

        return await GetAsync(id, cancellationToken);
    }

    public async Task<BackgroundJobModel> StartAsync(BackgroundJobServiceStartRequest request,
        CancellationToken cancellationToken = default)
    {
        var now = UtcNow;
        var leaseExpiresAtUtc = now.AddSeconds(options.Value.EffectiveLeaseSeconds);

        await using var context = await dbContextFactory.CreateDbContextAsync(ConnectionStringName, cancellationToken);

        /*
         * Conditional on the job still being pending at the leased attempt. If the lease expired and the job was leased
         * again, the attempt no longer matches and the start is refused, so the earlier lease never runs the job. The
         * lease is renewed from now, so it measures the run rather than the wait since the lease.
         */
        var updated = await context.BackgroundJobs
            .Where(e => e.Id == request.Id
                        && e.State == BackgroundJobState.Pending
                        && e.Attempts == request.Attempt
                        && e.LeaseExpiresAtUtc != null)
            .ExecuteUpdateAsync(setters => setters
                    .SetProperty(e => e.State, BackgroundJobState.Running)
                    .SetProperty(e => e.LeaseExpiresAtUtc, leaseExpiresAtUtc)
                    .SetProperty(e => e.DateUpdatedUtc, now),
                cancellationToken);

        return await GetReportedAsync(context, request.Id, updated, "started", cancellationToken);
    }
}
