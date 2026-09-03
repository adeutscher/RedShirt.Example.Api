using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Upload.Core.Models.Requests;
using RedShirt.Example.Api.Upload.Core.Models.Responses;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Commands.SubmitMoveReport;

public sealed record SubmitUploadMoveReportCommand(Guid UploadId, string VerifiedStorageObjectKey);

public interface
    ISubmitUploadMoveReportCommandHandler : ICqrsHandler<SubmitUploadMoveReportCommand, UploadSummaryModel>;

internal sealed class SubmitUploadMoveReportCommandHandler(
    IUploadService uploadService,
    ICoreRequestValidator coreRequestValidator) : ISubmitUploadMoveReportCommandHandler
{
    public async Task<UploadSummaryModel> Handle(SubmitUploadMoveReportCommand command,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(command, cancellationToken);
        return await uploadService.SubmitMoveReportAsync(new UploadServiceMoveReportRequest
        {
            UploadId = command.UploadId,
            VerifiedStorageObjectKey = command.VerifiedStorageObjectKey
        }, cancellationToken);
    }
}