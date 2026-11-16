using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Fail;

public interface IFailBackgroundJobCommandHandler : ICqrsHandler<FailBackgroundJobCommand, BackgroundJobModel>;

internal sealed class FailBackgroundJobCommandHandler(
    IBackgroundJobService backgroundJobService,
    ICoreRequestValidator coreRequestValidator) : IFailBackgroundJobCommandHandler
{
    public async Task<BackgroundJobModel> Handle(FailBackgroundJobCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);
        return await backgroundJobService.FailAsync(new BackgroundJobServiceFailRequest
        {
            Id = command.Id,
            Attempt = command.Attempt,
            CanRetry = command.CanRetry,
            Detail = command.Detail
        }, cancellationToken);
    }
}