using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Finish;

public interface IFinishBackgroundJobCommandHandler : ICqrsHandler<FinishBackgroundJobCommand, BackgroundJobModel>;

internal sealed class FinishBackgroundJobCommandHandler(
    IBackgroundJobService backgroundJobService,
    ICoreRequestValidator coreRequestValidator) : IFinishBackgroundJobCommandHandler
{
    public async Task<BackgroundJobModel> Handle(FinishBackgroundJobCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);
        return await backgroundJobService.FinishAsync(new BackgroundJobServiceFinishRequest
        {
            Id = command.Id,
            Attempt = command.Attempt,
            Detail = command.Detail
        }, cancellationToken);
    }
}