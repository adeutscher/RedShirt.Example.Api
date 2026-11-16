using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Logic.Services;
using RedShirt.Example.Api.Common.Exceptions.Responses;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Fail;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Finish;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Start;

namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Services;

/// <summary>
///     Reports the status of jobs run inside the API through the background job CQRS handlers. If the job logic is
///     migrated to an applied JobWorker, this is replaced there by API connector methods that call the API's background
///     job endpoints.
/// </summary>
internal sealed class CqrsBackgroundJobStatusService(
    IStartBackgroundJobCommandHandler startBackgroundJobCommandHandler,
    IFinishBackgroundJobCommandHandler finishBackgroundJobCommandHandler,
    IFailBackgroundJobCommandHandler failBackgroundJobCommandHandler) : IBackgroundJobStatusService
{
    /// <summary>
    ///     Runs a status report. A conflict means the job is not at this attempt any more (or not in a state for the
    ///     report), which is an expected outcome rather than an error.
    /// </summary>
    private static async Task<bool> ReportAsync(Func<Task> report)
    {
        try
        {
            await report();
            return true;
        }
        catch (ConflictException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Jobs from <see cref="CqrsBackgroundJobSource" /> are always <see cref="JobModel" />, which carries the attempt
    ///     number of the lease, and their message id is the BackgroundJob id.
    /// </summary>
    private static (Guid Id, int Attempt) Identify(IJobModel job)
    {
        if (job is not JobModel {Attempts: var attempt} || !Guid.TryParse(job.MessageId, out var id))
        {
            throw new ArgumentException($"Expected a {nameof(JobModel)} leased from the BackgroundJob table.",
                nameof(job));
        }

        return (id, attempt);
    }

    public Task<bool> ReportFailedAsync(IJobModel job, bool canRetry, string? detail,
        CancellationToken cancellationToken = default)
    {
        var (id, attempt) = Identify(job);
        return ReportAsync(() => failBackgroundJobCommandHandler.Handle(
            new FailBackgroundJobCommand(id, attempt, canRetry, detail), cancellationToken));
    }

    public Task<bool> ReportFinishedAsync(IJobModel job, string? detail,
        CancellationToken cancellationToken = default)
    {
        var (id, attempt) = Identify(job);
        return ReportAsync(() => finishBackgroundJobCommandHandler.Handle(
            new FinishBackgroundJobCommand(id, attempt, detail), cancellationToken));
    }

    public Task<bool> ReportStartedAsync(IJobModel job, CancellationToken cancellationToken = default)
    {
        var (id, attempt) = Identify(job);
        return ReportAsync(() => startBackgroundJobCommandHandler.Handle(
            new StartBackgroundJobCommand(id, attempt), cancellationToken));
    }
}