using Microsoft.Extensions.Options;
using RedShirt.Example.Api.BackgroundJobs.Core.Configuration;
using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Core.Services;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Acknowledge;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Lease;

namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Services;

/// <summary>
///     The in-API runner's job source. It reads and updates the BackgroundJob table through the same CQRS handlers as the
///     background job endpoints, so a JobWorker that polls those endpoints through a connector behaves the same way.
/// </summary>
internal sealed class CqrsBackgroundJobSource(
    ILeaseBackgroundJobsCommandHandler leaseBackgroundJobsCommandHandler,
    IAcknowledgeBackgroundJobCommandHandler acknowledgeBackgroundJobCommandHandler,
    IOptions<BackgroundJobOptions> options) : IBackgroundJobSource
{
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
        await acknowledgeBackgroundJobCommandHandler.Handle(
            new AcknowledgeBackgroundJobCommand(
                Guid.Parse(job.MessageId),
                options.Value.EffectiveProcessName,
                response.Result,
                (response as JobLogicRunnerResponse)?.Detail),
            cancellationToken);
    }
}
