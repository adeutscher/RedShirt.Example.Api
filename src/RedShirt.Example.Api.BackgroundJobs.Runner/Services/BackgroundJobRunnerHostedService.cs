using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RedShirt.Example.Api.BackgroundJobs.Core.Configuration;
using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Core.Services;

namespace RedShirt.Example.Api.BackgroundJobs.Runner.Services;

/// <summary>
///     A stripped-down version of the JobWorker template's short polling (batch mode): fetch a batch of jobs, run them
///     one at a time, acknowledge each, and back off while there is nothing to do.
///     <para>
///         Deliberately absent, compared to the JobWorker template: parallel handling, heartbeats (jobs are expected to
///         finish well within their lease), idempotency tracking, and loader mode. When a workload outgrows this,
///         I would strongly advise moving its job type to be handled an applied JobWorker instead of growing this class.
///     </para>
///     <para>
///         A job that is interrupted by shutdown is not acknowledged. Its lease expires and it is run again later.
///     </para>
/// </summary>
internal sealed class BackgroundJobRunnerHostedService(
    IServiceScopeFactory serviceScopeFactory,
    IBackgroundJobWakeSignal wakeSignal,
    IOptions<BackgroundJobRunnerOptions> options,
    ILogger<BackgroundJobRunnerHostedService> logger) : BackgroundService
{
    private async Task<IJobLogicRunnerResponse> RunJobAsync(IJobLogicRunner jobLogicRunner, IJobModel job,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await jobLogicRunner.RunAsync(job, cancellationToken);
            logger.LogInformation("{JobType} job {MessageId} finished as {Result}", job.Data.Type, job.MessageId,
                response.Result);
            return response;
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogError(e, "{JobType} job {MessageId} threw an exception", job.Data.Type, job.MessageId);
            return new JobLogicRunnerResponse
            {
                Result = JobResult.Failure,
                Detail = $"{e.GetType().Name}: {e.Message}"
            };
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.RunnerEnabled)
        {
            logger.LogInformation("Background job runner is disabled");
            return;
        }

        logger.LogInformation("Background job runner started");

        var idleRounds = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            int jobCount;
            try
            {
                jobCount = await RunBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                // Typically the job source being unavailable. Treat it as an empty poll, so the loop backs off.
                logger.LogError(e, "Unexpected error getting background jobs");
                jobCount = 0;
            }

            if (jobCount > 0)
            {
                idleRounds = 0;
                continue;
            }

            idleRounds++;
            var delay = GetIdleDelay(idleRounds);
            logger.LogTrace("No background jobs, waiting up to {Delay}", delay);

            try
            {
                if (await wakeSignal.WaitAsync(delay, stoppingToken))
                {
                    idleRounds = 0;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        logger.LogInformation("Background job runner stopped");
    }

    internal TimeSpan GetIdleDelay(int idleRounds)
    {
        var seconds = Math.Min(options.Value.EffectiveMaxIdleWaitSeconds, Math.Pow(2, Math.Clamp(idleRounds, 1, 30)));
        return TimeSpan.FromSeconds(seconds);
    }

    /// <summary>
    ///     Fetches one batch and runs it in sequence.
    /// </summary>
    /// <returns>The number of jobs fetched.</returns>
    internal async Task<int> RunBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var jobSource = scope.ServiceProvider.GetRequiredService<IBackgroundJobSource>();
        var jobLogicRunner = scope.ServiceProvider.GetRequiredService<IJobLogicRunner>();

        var jobs = await jobSource.GetJobsAsync(options.Value.EffectiveFetchCount, cancellationToken);
        foreach (var job in jobs)
        {
            var response = await RunJobAsync(jobLogicRunner, job, cancellationToken);

            try
            {
                await jobSource.AcknowledgeAsync(job, response, cancellationToken);
            }
            catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                // The lease will expire and the job will be run again.
                logger.LogError(e, "Failed to acknowledge {JobType} job {MessageId} as {Result}", job.Data.Type,
                    job.MessageId, response.Result);
            }
        }

        return jobs.Count;
    }
}