using RedShirt.Example.Api.BackgroundJobs.Logic.Handlers.MoveUpload;
using RedShirt.Example.Api.Common.Exceptions.Responses;
using RedShirt.Example.Api.Core.UseCases.Upload.Commands.CopyToVerifiedStorage;
using RedShirt.Example.Api.Core.UseCases.Upload.Commands.RemoveUnverifiedCopy;
using RedShirt.Example.Api.Core.UseCases.Upload.Commands.SubmitMoveReport;
using RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetSummary;
using RedShirt.Example.Api.Upload.Core.Models;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Services;

/// <summary>
///     The in-API implementation of what the upload move job needs, through the upload CQRS handlers.
/// </summary>
internal sealed class CqrsUploadStorageService(
    IGetUploadSummaryQueryHandler getUploadSummaryQueryHandler,
    ICopyUploadToVerifiedStorageCommandHandler copyUploadToVerifiedStorageCommandHandler,
    ISubmitUploadMoveReportCommandHandler submitUploadMoveReportCommandHandler,
    IRemoveUnverifiedUploadCopyCommandHandler removeUnverifiedUploadCopyCommandHandler) : IUploadStorageService
{
    public async Task<UploadMoveStage> GetMoveStageAsync(Guid uploadId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var summary = await getUploadSummaryQueryHandler.Handle(new GetUploadSummaryQuery(uploadId),
                cancellationToken);
            return summary.State switch
            {
                UploadState.Verified => UploadMoveStage.AwaitingMove,
                UploadState.Stored => UploadMoveStage.Stored,
                _ => UploadMoveStage.NotApplicable
            };
        }
        catch (ResourceNotFoundException)
        {
            // Purged since the job was submitted.
            return UploadMoveStage.NotApplicable;
        }
    }

    public Task<string> CopyToVerifiedStorageAsync(Guid uploadId, CancellationToken cancellationToken = default)
    {
        return copyUploadToVerifiedStorageCommandHandler.Handle(new CopyUploadToVerifiedStorageCommand(uploadId),
            cancellationToken);
    }

    public async Task SubmitMoveReportAsync(Guid uploadId, string verifiedStorageObjectKey,
        CancellationToken cancellationToken = default)
    {
        await submitUploadMoveReportCommandHandler.Handle(
            new SubmitUploadMoveReportCommand(uploadId, verifiedStorageObjectKey), cancellationToken);
    }

    public Task RemoveUnverifiedCopyAsync(Guid uploadId, CancellationToken cancellationToken = default)
    {
        return removeUnverifiedUploadCopyCommandHandler.Handle(new RemoveUnverifiedUploadCopyCommand(uploadId),
            cancellationToken);
    }
}