using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RedShirt.Example.Api.Attributes;
using RedShirt.Example.Api.Attributes.Authorization;
using RedShirt.Example.Api.Constants;
using RedShirt.Example.Api.Core.UseCases.Upload.Commands.Create;
using RedShirt.Example.Api.Core.UseCases.Upload.Commands.Delete;
using RedShirt.Example.Api.Core.UseCases.Upload.Commands.SubmitMoveReport;
using RedShirt.Example.Api.Core.UseCases.Upload.Commands.SubmitVerdict;
using RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetDetails;
using RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetDownloadLink;
using RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetSummary;
using RedShirt.Example.Api.Core.UseCases.Upload.Queries.SearchRecords;
using RedShirt.Example.Api.Models.Upload;
using RedShirt.Example.Api.Upload.Core.Models;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Controllers;

[ApiController]
[EnableRateLimiting(RateLimitingConstants.PolicyHeaderDefault)]
[Route("uploads")]
[ProducesJson]
public class UploadController : ControllerBase
{
    [HttpPost]
    [AuthorizeUploadWrite]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UploadSummaryModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Post(
        [FromHeader(Name = "X-File-Name")]
        string fileName,
        [FromServices]
        ICreateUploadCommandHandler createUploadCommandHandler,
        CancellationToken cancellationToken)
    {
        var model = await createUploadCommandHandler.Handle(
            new CreateUploadCommand(
                fileName,
                User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "anonymous",
                User.Identity?.Name ?? "anonymous",
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                Request.Body),
            cancellationToken);
        return Ok(model);
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
        CancellationToken cancellationToken)
    {
        var model = await getUploadSummaryQueryHandler.Handle(new GetUploadSummaryQuery(id), cancellationToken);
        return Ok(model);
    }

    [HttpGet]
    [ApproveUploadReadOnly]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UploadSearchResponse))]
    public async Task<IActionResult> Search(
        [FromQuery]
        UploadSearchRequest request,
        [FromServices]
        ISearchUploadRecordsQueryHandler searchUploadRecordsQueryHandler,
        CancellationToken cancellationToken)
    {
        var model = await searchUploadRecordsQueryHandler.Handle(
            new SearchUploadRecordsQuery(
                new UploadServiceSearchRequest
                {
                    PageSize = request.PageSize,
                    CreatedBeforeUtc = request.CreatedBeforeUtc,
                    CreatedAfterUtc = request.CreatedAfterUtc,
                    UpdatedBeforeUtc = request.UpdatedBeforeUtc,
                    UpdatedAfterUtc = request.UpdatedAfterUtc,
                    Id = request.Id,
                    State = request.State,
                    UploadedByUserId = request.UploadedByUserId,
                    FileName = request.FileName,
                    IsValidated = request.IsValidated,
                    IsRejected = request.IsRejected
                },
                request.ContinuationToken),
            cancellationToken);
        return Ok(model);
    }

    [HttpGet("{id:guid}/details")]
    [ApproveUploadReadOnly]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UploadDetailsModel))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetails(
        [FromRoute]
        Guid id,
        [FromServices]
        IGetUploadDetailsQueryHandler getUploadDetailsQueryHandler,
        CancellationToken cancellationToken)
    {
        var model = await getUploadDetailsQueryHandler.Handle(new GetUploadDetailsQuery(id), cancellationToken);
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

    [HttpDelete("{id:guid}")]
    [AuthorizeUploadWrite]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UploadSummaryModel))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute]
        Guid id,
        [FromServices]
        IDeleteUploadCommandHandler deleteUploadCommandHandler,
        CancellationToken cancellationToken)
    {
        var model = await deleteUploadCommandHandler.Handle(new DeleteUploadCommand(id), cancellationToken);
        return Ok(model);
    }

    [HttpGet("{id:guid}/download-link")]
    [ApproveUploadReadOnly]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(UploadDownloadLinkModel))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDownloadLink(
        [FromRoute]
        Guid id,
        [FromServices]
        IGetUploadDownloadLinkQueryHandler getUploadDownloadLinkQueryHandler,
        CancellationToken cancellationToken)
    {
        var model = await getUploadDownloadLinkQueryHandler.Handle(new GetUploadDownloadLinkQuery(id),
            cancellationToken);
        return Ok(model);
    }
}
