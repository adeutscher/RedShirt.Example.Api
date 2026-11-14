using Microsoft.Extensions.Logging;
using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.BackgroundJobs.Core.Models.Parameters;

namespace RedShirt.Example.Api.BackgroundJobs.Logic.Handlers.ValidateUpload;

/// <summary>
///     Checks a completed upload and submits a verdict. Replaces the mock <c>upload-validate-worker.py</c> script.
/// </summary>
internal sealed class ValidateUploadJobHandler(
    IUploadValidationService uploadValidationService,
    ILogger<ValidateUploadJobHandler> logger) : BackgroundJobHandler<ValidateUploadJobParameters>
{
    protected override async Task<IJobLogicRunnerResponse> HandleAsync(ValidateUploadJobParameters parameters,
        IJobModel job, CancellationToken cancellationToken)
    {
        if (parameters.UploadId == Guid.Empty)
        {
            return new JobLogicRunnerResponse
            {
                Result = JobResult.InvalidData,
                Detail = "Upload id cannot be empty."
            };
        }

        if (!await uploadValidationService.IsAwaitingValidationAsync(parameters.UploadId, cancellationToken))
        {
            // Nothing left to do. Treated as success so that a repeated job is harmless.
            return new JobLogicRunnerResponse
            {
                Result = JobResult.Success,
                Detail = "Upload is no longer awaiting validation."
            };
        }

        bool approved;
        await using (var content = await uploadValidationService.OpenContentAsync(parameters.UploadId,
                         cancellationToken))
        {
            approved = await UploadContentInspector.IsValidAsync(content, cancellationToken);
        }

        await uploadValidationService.SubmitVerdictAsync(parameters.UploadId, approved, cancellationToken);
        logger.LogInformation("Upload {UploadId} validated: approved={Approved}", parameters.UploadId, approved);

        return new JobLogicRunnerResponse
        {
            Result = JobResult.Success,
            Detail = approved ? "Approved." : $"Rejected: the document does not contain \"{UploadContentInspector.RequiredWord}\"."
        };
    }
}
