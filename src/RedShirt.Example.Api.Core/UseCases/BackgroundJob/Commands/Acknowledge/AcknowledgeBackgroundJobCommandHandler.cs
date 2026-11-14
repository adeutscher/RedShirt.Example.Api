using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Core.Services;
using RedShirt.Example.Api.Core.Cqrs;

namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Acknowledge;

public interface IAcknowledgeBackgroundJobCommandHandler
    : ICqrsHandler<AcknowledgeBackgroundJobCommand, BackgroundJobModel>;

internal sealed class AcknowledgeBackgroundJobCommandHandler(
    IBackgroundJobService backgroundJobService,
    ICoreRequestValidator coreRequestValidator) : IAcknowledgeBackgroundJobCommandHandler
{
    public async Task<BackgroundJobModel> Handle(AcknowledgeBackgroundJobCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);
        return await backgroundJobService.AcknowledgeAsync(new BackgroundJobServiceAcknowledgeRequest
        {
            Id = command.Id,
            LeaseOwner = command.LeaseOwner,
            Result = command.Result,
            Detail = command.Detail
        }, cancellationToken);
    }
}
