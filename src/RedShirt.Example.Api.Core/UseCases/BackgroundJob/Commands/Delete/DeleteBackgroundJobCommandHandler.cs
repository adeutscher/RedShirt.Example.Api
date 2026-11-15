using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Delete;

public interface IDeleteBackgroundJobCommandHandler : ICqrsHandler<DeleteBackgroundJobCommand>;

internal sealed class DeleteBackgroundJobCommandHandler(
    IBackgroundJobService backgroundJobService,
    ICoreRequestValidator coreRequestValidator) : IDeleteBackgroundJobCommandHandler
{
    public async Task Handle(DeleteBackgroundJobCommand command, CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);
        await backgroundJobService.DeleteAsync(command.Id, cancellationToken);
    }
}
