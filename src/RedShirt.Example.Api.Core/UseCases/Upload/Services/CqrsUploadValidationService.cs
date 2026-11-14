using RedShirt.Example.Api.BackgroundJobs.Logic.Handlers.ValidateUpload;
using RedShirt.Example.Api.Common.Exceptions.Responses;
using RedShirt.Example.Api.Core.UseCases.Upload.Commands.SubmitVerdict;
using RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetContent;
using RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetSummary;
using RedShirt.Example.Api.Upload.Core.Models;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Services;

/// <summary>
///     The in-API implementation of what the upload validation job needs, through the upload CQRS handlers.
/// </summary>
internal sealed class CqrsUploadValidationService(
    IGetUploadSummaryQueryHandler getUploadSummaryQueryHandler,
    IGetUploadContentQueryHandler getUploadContentQueryHandler,
    ISubmitUploadVerdictCommandHandler submitUploadVerdictCommandHandler) : IUploadValidationService
{
    public async Task<bool> IsAwaitingValidationAsync(Guid uploadId, CancellationToken cancellationToken = default)
    {
        try
        {
            var summary = await getUploadSummaryQueryHandler.Handle(new GetUploadSummaryQuery(uploadId),
                cancellationToken);
            return summary.State == UploadState.NotValidated;
        }
        catch (ResourceNotFoundException)
        {
            // Purged since the job was submitted.
            return false;
        }
    }

    public Task<Stream> OpenContentAsync(Guid uploadId, CancellationToken cancellationToken = default)
    {
        return getUploadContentQueryHandler.Handle(new GetUploadContentQuery(uploadId), cancellationToken);
    }

    public async Task SubmitVerdictAsync(Guid uploadId, bool approved, CancellationToken cancellationToken = default)
    {
        await submitUploadVerdictCommandHandler.Handle(new SubmitUploadVerdictCommand(uploadId, approved),
            cancellationToken);
    }
}
