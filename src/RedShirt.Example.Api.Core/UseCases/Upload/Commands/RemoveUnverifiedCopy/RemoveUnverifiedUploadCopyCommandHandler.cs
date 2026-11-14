using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Commands.RemoveUnverifiedCopy;

public sealed record RemoveUnverifiedUploadCopyCommand(Guid UploadId);

/// <summary>
///     Removes a stored upload's leftover unverified copy. Not exposed as an endpoint: an outside worker deletes with
///     storage APIs directly.
/// </summary>
public interface IRemoveUnverifiedUploadCopyCommandHandler : ICqrsHandler<RemoveUnverifiedUploadCopyCommand>;

internal sealed class RemoveUnverifiedUploadCopyCommandHandler(
    IUploadService uploadService,
    ICoreRequestValidator coreRequestValidator) : IRemoveUnverifiedUploadCopyCommandHandler
{
    public async Task Handle(RemoveUnverifiedUploadCopyCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);
        await uploadService.RemoveUnverifiedCopyAsync(command.UploadId, cancellationToken);
    }
}
