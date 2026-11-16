using Microsoft.Extensions.Options;
using RedShirt.Example.Api.BackgroundJobs.Core.Configuration;
using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Core.Services;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Lease;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;

namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Services;

/// <summary>
///     The in-API runner's job source. It leases jobs from the BackgroundJob table through a CQRS handler. The job logic
///     reports each job's status itself (see <see cref="CqrsBackgroundJobStatusService" />). An applied JobWorker does not
///     use this: it gets its jobs from a queue.
/// </summary>
internal sealed class CqrsBackgroundJobSource(
    ILeaseBackgroundJobsCommandHandler leaseBackgroundJobsCommandHandler,
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