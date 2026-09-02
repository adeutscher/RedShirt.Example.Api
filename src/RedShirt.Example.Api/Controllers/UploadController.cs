using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RedShirt.Example.Api.Attributes;
using RedShirt.Example.Api.Attributes.Authorization;
using RedShirt.Example.Api.Authorization.Constants;
using RedShirt.Example.Api.Authorization.Extensions;
using RedShirt.Example.Api.Authorization.ResourceScoping.Upload;
using RedShirt.Example.Api.Constants;
using RedShirt.Example.Api.Core.UseCases.Upload.Commands.Create;
using RedShirt.Example.Api.Core.UseCases.Upload.Commands.Delete;
using RedShirt.Example.Api.Core.UseCases.Upload.Commands.SubmitMoveReport;
using RedShirt.Example.Api.Core.UseCases.Upload.Commands.SubmitVerdict;
using RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetDetails;
using RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetDownloadLink;
using RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetInternalDetails;
using RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetSummary;
using RedShirt.Example.Api.Core.UseCases.Upload.Queries.SearchRecords;
using RedShirt.Example.Api.Models.Upload;
using RedShirt.Example.Api.Upload.Core.Models.Responses;

namespace RedShirt.Example.Api.Controllers;

[ApiController]
[EnableRateLimiting(RateLimitingConstants.PolicyHeaderDefault)]
[Route("uploads")]
[ProducesJson]
public class UploadController : ControllerBase
{
    [HttpDelete("{id:guid}")]
    [AuthorizeUploadWrite]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UploadSummaryModel))]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute]
        Guid id,
        [FromQuery]
        bool purge,
        [FromServices]
        IGetUploadSummaryQueryHandler getUploadSummaryQueryHandler,
        [FromServices]
        IUploadScopedResourceEnforcer uploadScopedResourceEnforcer,
        [FromServices]
        IDeleteUploadCommandHandler deleteUploadCommandHandler,
        CancellationToken cancellationToken)
    {
        var existing = await getUploadSummaryQueryHandler.Handle(new GetUploadSummaryQuery(id), cancellationToken);

        if (purge)
        {
            if (!User.HasClaim(BespokeAuthorizationPermissions.ClaimType, BespokeAuthorizationPermissions.UploadPurge))
            {
                return Forbid();
            }
        }
        else
        {
            await uploadScopedResourceEnforcer.EnsureCanAccessAsync(User, existing.UploadedByUserId);
        }

        var model = await deleteUploadCommandHandler.Handle(new DeleteUploadCommand(id, purge), cancellationToken);
        return model is null ? NoContent() : Ok(model);
    }

    [HttpGet("{id:guid}")]
    [AuthorizeUploadReadOrValidator]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UploadSummaryModel))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        [FromRoute]
        Guid id,
        [FromServices]
        IGetUploadSummaryQueryHandler getUploadSummaryQueryHandler,
        [FromServices]
        IUploadScopedResourceEnforcer uploadScopedResourceEnforcer,
        CancellationToken cancellationToken)
    {
        var model = await getUploadSummaryQueryHandler.Handle(new GetUploadSummaryQuery(id), cancellationToken);
        await uploadScopedResourceEnforcer.EnsureCanAccessAsync(User, model.UploadedByUserId, true);
        return Ok(model);
    }

    [HttpGet("{id:guid}/details")]
    [AuthorizeUploadReadOrValidator]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UploadDetailsModel))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetails(
        [FromRoute]
        Guid id,
        [FromServices]
        IGetUploadSummaryQueryHandler getUploadSummaryQueryHandler,
        [FromServices]
        IUploadScopedResourceEnforcer uploadScopedResourceEnforcer,
        [FromServices]
        IGetUploadDetailsQueryHandler getUploadDetailsQueryHandler,
        CancellationToken cancellationToken)
    {
        var summary = await getUploadSummaryQueryHandler.Handle(new GetUploadSummaryQuery(id), cancellationToken);
        await uploadScopedResourceEnforcer.EnsureCanAccessAsync(User, summary.UploadedByUserId, false);
        var model = await getUploadDetailsQueryHandler.Handle(new GetUploadDetailsQuery(id), cancellationToken);
        return Ok(model);
    }

    [HttpGet("{id:guid}/download-link")]
    [ApproveUploadDownloadLink]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UploadDownloadLinkModel))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDownloadLink(
        [FromRoute]
        Guid id,
        [FromServices]
        IGetUploadSummaryQueryHandler getUploadSummaryQueryHandler,
        [FromServices]
        IUploadScopedResourceEnforcer uploadScopedResourceEnforcer,
        [FromServices]
        IGetUploadDownloadLinkQueryHandler getUploadDownloadLinkQueryHandler,
        CancellationToken cancellationToken)
    {
        var summary = await getUploadSummaryQueryHandler.Handle(new GetUploadSummaryQuery(id), cancellationToken);
        await uploadScopedResourceEnforcer.EnsureCanDownloadAsync(User, summary.UploadedByUserId, summary.State);
        var model = await getUploadDownloadLinkQueryHandler.Handle(new GetUploadDownloadLinkQuery(id),
            cancellationToken);
        return Ok(model);
    }

    [HttpGet("{id:guid}/details/internal")]
    [ApproveUploadInternalDetails]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UploadInternalDetailsModel))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetInternalDetails(
        [FromRoute]
        Guid id,
        [FromServices]
        IGetUploadInternalDetailsQueryHandler getUploadInternalDetailsQueryHandler,
        CancellationToken cancellationToken)
    {
        var model = await getUploadInternalDetailsQueryHandler.Handle(
            new GetUploadInternalDetailsQuery(id),
            cancellationToken);
        // Reminder: Enforcing scope within the endpoint isn't necessary because only internal processes can get at this.
        return Ok(model);
    }

    [HttpPost]
    [AuthorizeUploadWrite]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UploadSummaryModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Post(
        [FromHeader(Name = "X-File-Name")]
        string fileName,
        [FromHeader(Name = EndpointConstants.IdempotencyKeyHeader)]
        string idempotencyKey,
        [FromServices]
        ICreateUploadCommandHandler createUploadCommandHandler,
        CancellationToken cancellationToken)
    {
        var model = await createUploadCommandHandler.Handle(
            new CreateUploadCommand(
                fileName,
                User.TryGetUserId(out var uploadedByUserId) ? uploadedByUserId : CallerConstants.Anonymous,
                User.Identity?.Name ?? CallerConstants.Anonymous,
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                Request.Body,
                Request.ContentLength,
                string.IsNullOrWhiteSpace(idempotencyKey) ? Guid.NewGuid().ToString() : idempotencyKey),
            cancellationToken);
        return Ok(model);
    }

    [HttpGet]
    [ApproveUploadReadOnly]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UploadSearchResponse))]
    public async Task<IActionResult> Search(
        [FromQuery]
        UploadSearchRequest request,
        [FromServices]
        IUploadScopedResourceEnforcer uploadScopedResourceEnforcer,
        [FromServices]
        ISearchUploadRecordsQueryHandler searchUploadRecordsQueryHandler,
        CancellationToken cancellationToken)
    {
        var uploadedByUserId =
            uploadScopedResourceEnforcer.ConstrainSearchUploadedByUserId(User, request.UploadedByUserId);
        var model = await searchUploadRecordsQueryHandler.Handle(
            new SearchUploadRecordsQuery(
                request.PageSize,
                request.CreatedBeforeUtc,
                request.CreatedAfterUtc,
                request.UpdatedBeforeUtc,
                request.UpdatedAfterUtc,
                request.Id,
                request.State,
                uploadedByUserId,
                request.FileName,
                request.IsValidated,
                request.IsRejected,
                request.ContinuationToken),
            cancellationToken);
        return Ok(model);
    }

    [HttpPost("{id:guid}/move-reports")]
    [AuthorizeUploadValidator]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UploadSummaryModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitMoveReport(
        [FromRoute]
        Guid id,
        [FromBody]
        UploadMoveReportRequest request,
        [FromServices]
        ISubmitUploadMoveReportCommandHandler submitUploadMoveReportCommandHandler,
        CancellationToken cancellationToken)
    {
        var model = await submitUploadMoveReportCommandHandler.Handle(
            new SubmitUploadMoveReportCommand(id, request.VerifiedStorageObjectKey),
            cancellationToken);
        return Ok(model);
    }

    [HttpPost("{id:guid}/verdicts")]
    [AuthorizeUploadValidator]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UploadSummaryModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SubmitVerdict(
        [FromRoute]
        Guid id,
        [FromBody]
        UploadVerdictRequest request,
        [FromServices]
        ISubmitUploadVerdictCommandHandler submitUploadVerdictCommandHandler,
        CancellationToken cancellationToken)
    {
        var model = await submitUploadVerdictCommandHandler.Handle(
            new SubmitUploadVerdictCommand(id, request.Approved),
            cancellationToken);
        return Ok(model);
    }
}