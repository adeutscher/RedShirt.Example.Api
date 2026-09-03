using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Upload.Core.Models.Requests;
using RedShirt.Example.Api.Upload.Core.Models.Responses;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Commands.SubmitVerdict;

public sealed record SubmitUploadVerdictCommand(Guid UploadId, bool Approved);

public interface ISubmitUploadVerdictCommandHandler : ICqrsHandler<SubmitUploadVerdictCommand, UploadSummaryModel>;

internal sealed class SubmitUploadVerdictCommandHandler(
    IUploadService uploadService,
    ICoreRequestValidator coreRequestValidator) : ISubmitUploadVerdictCommandHandler
{
    public async Task<UploadSummaryModel> Handle(SubmitUploadVerdictCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);
        return await uploadService.SubmitVerdictAsync(new UploadServiceVerdictRequest
        {
            UploadId = command.UploadId,
            Approved = command.Approved
        }, cancellationToken);
    }
}