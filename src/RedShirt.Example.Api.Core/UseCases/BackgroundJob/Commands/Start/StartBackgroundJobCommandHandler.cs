using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Start;

public interface IStartBackgroundJobCommandHandler : ICqrsHandler<StartBackgroundJobCommand, BackgroundJobModel>;

internal sealed class StartBackgroundJobCommandHandler(
    IBackgroundJobService backgroundJobService,
    ICoreRequestValidator coreRequestValidator) : IStartBackgroundJobCommandHandler
{
    public async Task<BackgroundJobModel> Handle(StartBackgroundJobCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);
        return await backgroundJobService.StartAsync(new BackgroundJobServiceStartRequest
        {
            Id = command.Id,
            Attempt = command.Attempt
        }, cancellationToken);
    }
}