using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Commands.CopyToVerifiedStorage;

public sealed record CopyUploadToVerifiedStorageCommand(Guid UploadId);

/// <summary>
///     Copies a verified upload's file into the verified bucket. Not exposed as an endpoint: an outside worker copies
///     with storage APIs directly, using the object key from GET <c>/uploads/{id}/details/internal</c>, and then
///     reports the move through POST <c>/uploads/{id}/move-reports</c>.
/// </summary>
/// <returns>The object key in the verified bucket.</returns>
public interface ICopyUploadToVerifiedStorageCommandHandler
    : ICqrsHandler<CopyUploadToVerifiedStorageCommand, string>;

internal sealed class CopyUploadToVerifiedStorageCommandHandler(
    IUploadService uploadService,
    ICoreRequestValidator coreRequestValidator) : ICopyUploadToVerifiedStorageCommandHandler
{
    public async Task<string> Handle(CopyUploadToVerifiedStorageCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);
        return await uploadService.CopyToVerifiedStorageAsync(command.UploadId, cancellationToken);
    }
}
