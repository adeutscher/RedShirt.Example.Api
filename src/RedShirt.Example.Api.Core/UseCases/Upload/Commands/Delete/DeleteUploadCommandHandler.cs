using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Upload.Core.Models.Responses;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Commands.Delete;

public sealed record DeleteUploadCommand(Guid Id, bool Purge = false);

public interface IDeleteUploadCommandHandler : ICqrsHandler<DeleteUploadCommand, UploadSummaryModel?>;

internal sealed class DeleteUploadCommandHandler(
    IUploadService uploadService,
    ICoreRequestValidator coreRequestValidator) : IDeleteUploadCommandHandler
{
    public async Task<UploadSummaryModel?> Handle(DeleteUploadCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);

        if (command.Purge)
        {
            await uploadService.PurgeAsync(command.Id, cancellationToken);
            return null;
        }

        return await uploadService.DeleteAsync(command.Id, cancellationToken);
    }
}