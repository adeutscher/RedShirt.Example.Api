using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Lease;

public interface ILeaseBackgroundJobsCommandHandler
    : ICqrsHandler<LeaseBackgroundJobsCommand, IReadOnlyList<BackgroundJobModel>>;

internal sealed class LeaseBackgroundJobsCommandHandler(
    IBackgroundJobService backgroundJobService,
    ICoreRequestValidator coreRequestValidator) : ILeaseBackgroundJobsCommandHandler
{
    public async Task<IReadOnlyList<BackgroundJobModel>> Handle(LeaseBackgroundJobsCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);
        return await backgroundJobService.LeaseAsync(new BackgroundJobServiceLeaseRequest
        {
            Count = command.Count,
            LeaseOwner = command.LeaseOwner
        }, cancellationToken);
    }
}