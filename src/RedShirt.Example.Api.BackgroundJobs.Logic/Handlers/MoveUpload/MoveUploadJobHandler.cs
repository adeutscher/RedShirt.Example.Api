using Microsoft.Extensions.Logging;
using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Submission.Models.Parameters;

namespace RedShirt.Example.Api.BackgroundJobs.Logic.Handlers.MoveUpload;

/// <summary>
///     Moves a verified upload into the verified bucket. A worker outside the API does the same with GET
///     <c>/uploads/{id}</c>, GET <c>/uploads/{id}/details/internal</c> (for the object key), storage APIs for the copy
///     and delete, and POST <c>/uploads/{id}/move-reports</c>.
///     <para>
///         The order is copy, report, then delete, so every step can be repeated: a failure before the report leaves the
///         upload verified and the copy is simply made again, and a failure after the report leaves the upload stored,
///         where a retry only finishes the cleanup. Deleting before reporting would leave a retry with nothing to copy.
///     </para>
/// </summary>
internal sealed class MoveUploadJobHandler(
    IUploadStorageService uploadStorageService,
    ILogger<MoveUploadJobHandler> logger) : BackgroundJobHandler<MoveUploadJobParameters>
{
    protected override async Task<IJobLogicRunnerResponse> HandleAsync(MoveUploadJobParameters parameters,
        IJobModel job, CancellationToken cancellationToken)
    {
        var uploadId = parameters.UploadId;
        if (uploadId == Guid.Empty)
        {
            return new JobLogicRunnerResponse
            {
                Result = JobResult.InvalidData,
                Detail = "Upload id cannot be empty."
            };
        }

        var stage = await uploadStorageService.GetMoveStageAsync(uploadId, cancellationToken);
        switch (stage)
        {
            case UploadMoveStage.AwaitingMove:
                var verifiedKey = await uploadStorageService.CopyToVerifiedStorageAsync(uploadId, cancellationToken);
                await uploadStorageService.SubmitMoveReportAsync(uploadId, verifiedKey, cancellationToken);
                await uploadStorageService.RemoveUnverifiedCopyAsync(uploadId, cancellationToken);
                logger.LogInformation("Upload {UploadId} moved to verified storage", uploadId);
                return new JobLogicRunnerResponse
                {
                    Result = JobResult.Success,
                    Detail = "Moved."
                };
            case UploadMoveStage.Stored:
                // A repeated job, or a retry after the report: make sure the unverified copy is gone.
                await uploadStorageService.RemoveUnverifiedCopyAsync(uploadId, cancellationToken);
                return new JobLogicRunnerResponse
                {
                    Result = JobResult.Success,
                    Detail = "Upload was already stored. Removed any unverified copy."
                };
            default:
                return new JobLogicRunnerResponse
                {
                    Result = JobResult.Success,
                    Detail = "Upload is no longer awaiting a move."
                };
        }
    }
}
