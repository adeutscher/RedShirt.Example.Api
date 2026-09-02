using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.Services;
using RedShirt.Example.Api.Upload.Core.Models;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Commands.Delete;

public sealed record DeleteUploadCommand(Guid Id);

public interface IDeleteUploadCommandHandler : ICqrsHandler<DeleteUploadCommand, UploadSummaryModel>;

internal sealed class DeleteUploadCommandHandler(
    IUploadService uploadService,
    ICoreRequestValidator coreRequestValidator) : IDeleteUploadCommandHandler
{
    public async Task<UploadSummaryModel> Handle(DeleteUploadCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);
        return await uploadService.DeleteAsync(command.Id, cancellationToken);
    }
}
