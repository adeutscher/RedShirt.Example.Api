using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Logic.Services;
using RedShirt.Example.Api.BackgroundJobs.Submission.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Submission.Core.Models.Parameters;
using RedShirt.Example.Api.BackgroundJobs.Submission.Core.Services;
using System.Text.Json;

namespace RedShirt.Example.Api.BackgroundJobs.Logic.Handlers;

/// <summary>
///     Runs one type of background job.
/// </summary>
public interface IBackgroundJobHandler
{
    BackgroundJobType Type { get; }

    /// <summary>
    ///     Runs the job and reports its status. The returned response is only for handling the job's message (in an
    ///     applied JobWorker, whether the queue message is completed or delivered again).
    /// </summary>
    Task<IJobLogicRunnerResponse> HandleAsync(IJobModel job, CancellationToken cancellationToken = default);
}

/// <summary>
///     Reports the job's start, reads its parameters model, hands over to
///     <see cref="HandleAsync(TParameters, IJobModel, CancellationToken)" />, and reports the outcome.
///     Parameters that do not read are <see cref="JobResult.InvalidData" />, since no retry can fix them. An exception is
///     a <see cref="JobResult.Failure" />, tried again later.
/// </summary>
public abstract class BackgroundJobHandler<TParameters>(IBackgroundJobStatusService statusService)
    : IBackgroundJobHandler
    where TParameters : class, IBackgroundJobParameters
{
    private async Task<IJobLogicRunnerResponse> RunAsync(IJobModel job, CancellationToken cancellationToken)
    {
        TParameters? parameters;
        try
        {
            parameters = JsonSerializer.Deserialize<TParameters>(job.Data.ParametersJson, BackgroundJobJson.Options);
        }
        catch (JsonException e)
        {
            return new JobLogicRunnerResponse
            {
                Result = JobResult.InvalidData,
                Detail = $"The {Type} parameters could not be read: {e.Message}"
            };
        }

        if (parameters is null)
        {
            return new JobLogicRunnerResponse
            {
                Result = JobResult.InvalidData,
                Detail = $"The {Type} parameters are empty."
            };
        }

        return await HandleAsync(parameters, job, cancellationToken);
    }

    public BackgroundJobType Type => TParameters.JobType;

    public async Task<IJobLogicRunnerResponse> HandleAsync(IJobModel job,
        CancellationToken cancellationToken = default)
    {
        /*
         * The status reports in this method (through IBackgroundJobStatusService) go to the background job CQRS handlers
         * inside the API. If this logic runner is migrated to an applied JobWorker, they need to be replaced with API
         * connector methods.
         *
         * The intent of reporting status here, rather than from the runner, is to make that migration easier: the job
         * logic already reports to the BackgroundJob record itself, as it would have to from a JobWorker, and the
         * runner is left with only the message handling.
         */
        if (!await statusService.ReportStartedAsync(job, cancellationToken))
        {
            // Another attempt owns the job now. Success, so that in a JobWorker this duplicate message is completed.
            return new JobLogicRunnerResponse
            {
                Result = JobResult.Success,
                Detail = "The job was not started: it is no longer at this attempt or waiting to start."
            };
        }

        IJobLogicRunnerResponse response;
        try
        {
            response = await RunAsync(job, cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            response = new JobLogicRunnerResponse
            {
                Result = JobResult.Failure,
                Detail = $"{e.GetType().Name}: {e.Message}"
            };
        }

        // Cancelled (shutting down): nothing is reported. The lease expires and the job is run again later.

        var detail = (response as JobLogicRunnerResponse)?.Detail;
        // A false return means another attempt has taken the job over and reports the outcome instead.
        _ = response.Result switch
        {
            JobResult.Success => await statusService.ReportFinishedAsync(job, detail, cancellationToken),
            JobResult.InvalidData => await statusService.ReportFailedAsync(job, false, detail, cancellationToken),
            _ => await statusService.ReportFailedAsync(job, true, detail, cancellationToken)
        };

        return response;
    }

    protected abstract Task<IJobLogicRunnerResponse> HandleAsync(TParameters parameters, IJobModel job,
        CancellationToken cancellationToken);
}