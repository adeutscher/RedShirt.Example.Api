using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RedShirt.Example.Api.Attributes;
using RedShirt.Example.Api.Attributes.Authorization;
using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using RedShirt.Example.Api.Constants;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Acknowledge;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Lease;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Retry;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Queries.GetRecord;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Queries.SearchRecords;
using RedShirt.Example.Api.Models.BackgroundJob;

namespace RedShirt.Example.Api.Controllers;

/// <summary>
///     Background job status, plus lease and result endpoints for a worker outside the API. The in-API runner uses the
///     same CQRS handlers as these endpoints, so a JobWorker can take over job types by polling here.
/// </summary>
[ApiController]
[EnableRateLimiting(RateLimitingConstants.PolicyHeaderDefault)]
[Route("background-jobs")]
[ProducesJson]
public class BackgroundJobController : ControllerBase
{
    [HttpGet("{id:guid}")]
    [ApproveBackgroundJobReadOnly]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(BackgroundJobModel))]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        [FromRoute]
        Guid id,
        [FromServices]
        IGetBackgroundJobRecordQueryHandler getBackgroundJobRecordQueryHandler,
        CancellationToken cancellationToken)
    {
        var model = await getBackgroundJobRecordQueryHandler.Handle(new GetBackgroundJobRecordQuery(id),
            cancellationToken);
        return Ok(model);
    }

    [HttpPost("leases")]
    [AuthorizeBackgroundJobWorker]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IReadOnlyList<BackgroundJobModel>))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Lease(
        [FromBody]
        BackgroundJobLeaseRequest request,
        [FromServices]
        ILeaseBackgroundJobsCommandHandler leaseBackgroundJobsCommandHandler,
        CancellationToken cancellationToken)
    {
        var model = await leaseBackgroundJobsCommandHandler.Handle(
            new LeaseBackgroundJobsCommand(request.Count, request.LeaseOwner),
            cancellationToken);
        return Ok(model);
    }

    [HttpPost("{id:guid}/retries")]
    [AuthorizeBackgroundJobWrite]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(BackgroundJobModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Retry(
        [FromRoute]
        Guid id,
        [FromServices]
        IRetryBackgroundJobCommandHandler retryBackgroundJobCommandHandler,
        CancellationToken cancellationToken)
    {
        var model = await retryBackgroundJobCommandHandler.Handle(new RetryBackgroundJobCommand(id),
            cancellationToken);
        return Ok(model);
    }

    [HttpGet]
    [ApproveBackgroundJobReadOnly]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(BackgroundJobSearchResponse))]
    public async Task<IActionResult> Search(
        [FromQuery]
        BackgroundJobSearchRequest request,
        [FromServices]
        ISearchBackgroundJobRecordsQueryHandler searchBackgroundJobRecordsQueryHandler,
        CancellationToken cancellationToken)
    {
        var model = await searchBackgroundJobRecordsQueryHandler.Handle(
            new SearchBackgroundJobRecordsQuery(
                request.PageSize,
                request.Type,
                request.State,
                request.CreatedBeforeUtc),
            cancellationToken);
        return Ok(model);
    }

    [HttpPost("{id:guid}/results")]
    [AuthorizeBackgroundJobWorker]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(BackgroundJobModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SubmitResult(
        [FromRoute]
        Guid id,
        [FromBody]
        BackgroundJobResultRequest request,
        [FromServices]
        IAcknowledgeBackgroundJobCommandHandler acknowledgeBackgroundJobCommandHandler,
        CancellationToken cancellationToken)
    {
        var model = await acknowledgeBackgroundJobCommandHandler.Handle(
            new AcknowledgeBackgroundJobCommand(id, request.LeaseOwner, request.Result, request.Detail),
            cancellationToken);
        return Ok(model);
    }
}
