using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedShirt.Example.Api.BackgroundJobs.Cleanup.Configuration;
using RedShirt.Example.Api.Common.Exceptions.Responses;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Delete;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Queries.SearchRecords;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;

namespace RedShirt.Example.Api.BackgroundJobs.Cleanup.Services;

/// <summary>
///     Periodically deletes succeeded and failed jobs that finished longer ago than the configured retention period. It
///     finds them with the background job search query and deletes them one at a time with the delete command.
///     <para>
///         Every API instance with cleanup enabled runs this. That is safe: a job that another instance already deleted,
///         or that was retried in the meantime, is skipped.
///     </para>
/// </summary>
internal sealed class BackgroundJobCleanupHostedService(
    IServiceScopeFactory serviceScopeFactory,
    TimeProvider timeProvider,
    IOptions<BackgroundJobCleanupOptions> options,
    ILogger<BackgroundJobCleanupHostedService> logger) : BackgroundService
{
    internal const int PageSize = 100;
    internal static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private static readonly BackgroundJobState[] FinishedStates =
        [BackgroundJobState.Succeeded, BackgroundJobState.Failed];

    private async Task<bool> TryDeleteAsync(IDeleteBackgroundJobCommandHandler deleteHandler, Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            await deleteHandler.Handle(new DeleteBackgroundJobCommand(id), cancellationToken);
            return true;
        }
        catch (Exception e) when (e is ResourceNotFoundException or BadRequestException or ConflictException)
        {
            // Deleted by another instance, or retried since the search. Either way it is not ours to delete now.
            logger.LogDebug("Skipped deleting background job {Id}: {Reason}", id, e.GetType().Name);
            return false;
        }
    }

    /// <summary>
    ///     Deletes every succeeded and failed job that finished before the retention cutoff.
    /// </summary>
    /// <returns>The number of jobs deleted.</returns>
    internal async Task<int> CleanUpAsync(CancellationToken cancellationToken)
    {
        var retentionDays = options.Value.CleanupRetentionDays
                            ?? throw new InvalidOperationException("Background job cleanup retention is not set.");
        var cutoff = timeProvider.GetUtcNow().UtcDateTime.AddDays(-retentionDays);

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var searchHandler = scope.ServiceProvider.GetRequiredService<ISearchBackgroundJobRecordsQueryHandler>();
        var deleteHandler = scope.ServiceProvider.GetRequiredService<IDeleteBackgroundJobCommandHandler>();

        var total = 0;
        foreach (var state in FinishedStates)
        {
            var deleted = 0;
            while (true)
            {
                // Deleted jobs drop out of the results, so each search returns the next page.
                var page = await searchHandler.Handle(
                    new SearchBackgroundJobRecordsQuery(PageSize, null, state, null, cutoff),
                    cancellationToken);
                if (page.Records.Count == 0)
                {
                    break;
                }

                var deletedFromPage = 0;
                foreach (var job in page.Records)
                {
                    if (await TryDeleteAsync(deleteHandler, job.Id, cancellationToken))
                    {
                        deletedFromPage++;
                    }
                }

                deleted += deletedFromPage;

                // Guards against searching forever when every job on a page is skipped.
                if (deletedFromPage == 0)
                {
                    break;
                }
            }

            if (deleted > 0)
            {
                logger.LogInformation("Deleted {Count} {State} background jobs that finished before {Cutoff:O}",
                    deleted, state, cutoff);
            }

            total += deleted;
        }

        return total;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.CleanupEnabled)
        {
            logger.LogInformation("Background job cleanup is disabled");
            return;
        }

        logger.LogInformation("Background job cleanup started, keeping finished jobs for {RetentionDays} days",
            options.Value.CleanupRetentionDays);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanUpAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                // Typically the database being unavailable. The next run tries again.
                logger.LogError(e, "Unexpected error cleaning up background jobs");
            }

            try
            {
                await Task.Delay(Interval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation("Background job cleanup stopped");
    }
}