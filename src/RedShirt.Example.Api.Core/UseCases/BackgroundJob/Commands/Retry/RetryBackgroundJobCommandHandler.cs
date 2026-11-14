using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Core.Services;
using RedShirt.Example.Api.Core.Cqrs;

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
