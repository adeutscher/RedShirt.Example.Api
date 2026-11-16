using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Retry;

public interface IRetryBackgroundJobCommandHandler : ICqrsHandler<RetryBackgroundJobCommand, BackgroundJobModel>;

internal sealed class RetryBackgroundJobCommandHandler(
    IBackgroundJobService backgroundJobService,
    ICoreRequestValidator coreRequestValidator) : IRetryBackgroundJobCommandHandler
{
    public async Task<BackgroundJobModel> Handle(RetryBackgroundJobCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);
        return await backgroundJobService.RetryAsync(command.Id, cancellationToken);
    }
}