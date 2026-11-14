using RedShirt.Example.Api.BackgroundJobs.Core.Models;
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

    Task<IJobLogicRunnerResponse> HandleAsync(IJobModel job, CancellationToken cancellationToken = default);
}

/// <summary>
///     Reads the job's parameters model before handing over to <see cref="HandleAsync(TParameters, IJobModel, CancellationToken)" />.
///     Parameters that do not read are <see cref="JobResult.InvalidData" />, since no retry can fix them.
/// </summary>
public abstract class BackgroundJobHandler<TParameters> : IBackgroundJobHandler
    where TParameters : class, IBackgroundJobParameters
{
    public BackgroundJobType Type => TParameters.JobType;

    public async Task<IJobLogicRunnerResponse> HandleAsync(IJobModel job,
        CancellationToken cancellationToken = default)
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

    protected abstract Task<IJobLogicRunnerResponse> HandleAsync(TParameters parameters, IJobModel job,
        CancellationToken cancellationToken);
}
