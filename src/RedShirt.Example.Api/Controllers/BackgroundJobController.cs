using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RedShirt.Example.Api.Attributes;
using RedShirt.Example.Api.Attributes.Authorization;
using RedShirt.Example.Api.Constants;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Retry;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Queries.GetRecord;
using RedShirt.Example.Api.Core.UseCases.BackgroundJob.Queries.SearchRecords;
using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;
using RedShirt.Example.Api.Models.BackgroundJob;

namespace RedShirt.Example.Api.Controllers;

/// <summary>
///     Background job status, and retries of failed jobs. Jobs are only ever taken for execution by the in-API runner
///     (through CQRS handlers) or, once job types move to an applied JobWorker, from that worker's queue. So there are
///     deliberately no endpoints for leasing jobs or reporting their outcomes.
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
}
