using Microsoft.Extensions.Options;
using RedShirt.Example.Api.BackgroundJobs.Core.Configuration;
using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Core.Services;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Acknowledge;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Lease;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;

namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Services;

/// <summary>
///     The in-API runner's job source. It leases jobs from the BackgroundJob table and records their outcomes through
///     CQRS handlers. An applied JobWorker does not use this: it gets its jobs from a queue.
/// </summary>
internal sealed class CqrsBackgroundJobSource(
    ILeaseBackgroundJobsCommandHandler leaseBackgroundJobsCommandHandler,
    IAcknowledgeBackgroundJobCommandHandler acknowledgeBackgroundJobCommandHandler,
    IOptions<BackgroundJobRunnerOptions> options) : IBackgroundJobSource
{
    public async Task<IReadOnlyList<IJobModel>> GetJobsAsync(int batchSize,
        CancellationToken cancellationToken = default)
    {
        var leased = await leaseBackgroundJobsCommandHandler.Handle(
            new LeaseBackgroundJobsCommand(batchSize, options.Value.EffectiveProcessName),
            cancellationToken);
        return leased.Select(model => (IJobModel) ToJobModel(model)).ToList();
    }

    public async Task AcknowledgeAsync(IJobModel job, IJobLogicRunnerResponse response,
        CancellationToken cancellationToken = default)
    {
        // Jobs from this source are always JobModel (see GetJobsAsync), which carries the attempt number of the lease.
        if (job is not JobModel {Attempts: var attempt})
        {
            throw new ArgumentException($"Expected a {nameof(JobModel)} leased by this source.", nameof(job));
        }

        await acknowledgeBackgroundJobCommandHandler.Handle(
            new AcknowledgeBackgroundJobCommand(
                Guid.Parse(job.MessageId),
                options.Value.EffectiveProcessName,
                attempt,
                ToAttemptResult(response.Result),
                (response as JobLogicRunnerResponse)?.Detail),
            cancellationToken);
    }

    internal static BackgroundJobAttemptResult ToAttemptResult(JobResult result)
    {
        return result switch
        {
            JobResult.Success => BackgroundJobAttemptResult.Success,
            JobResult.Failure => BackgroundJobAttemptResult.Failure,
            JobResult.InvalidData => BackgroundJobAttemptResult.InvalidData,
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown job result.")
        };
    }

    internal static JobModel ToJobModel(BackgroundJobModel model)
    {
        return new JobModel
        {
            MessageId = model.Id.ToString(),
            IdempotencyId = model.Id.ToString(),
            CreatedAtUtc = model.DateCreatedUtc,
            Attempts = model.Attempts,
            Data = new JobDataModel
            {
                Type = model.Type,
                ParametersJson = model.ParametersJson
            }
        };
    }
}