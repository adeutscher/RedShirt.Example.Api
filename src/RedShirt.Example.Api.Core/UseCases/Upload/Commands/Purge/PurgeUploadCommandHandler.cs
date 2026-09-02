using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Commands.Purge;

public sealed record PurgeUploadCommand(Guid Id);

public interface IPurgeUploadCommandHandler : ICqrsHandler<PurgeUploadCommand>;

internal sealed class PurgeUploadCommandHandler(
    IUploadService uploadService,
    ICoreRequestValidator coreRequestValidator) : IPurgeUploadCommandHandler
{
    public async Task Handle(PurgeUploadCommand command, CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);
        await uploadService.PurgeAsync(command.Id, cancellationToken);
    }
}
