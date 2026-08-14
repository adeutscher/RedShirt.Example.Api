using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RedShirt.Example.Api.Attributes;
using RedShirt.Example.Api.Attributes.Authorization;
using RedShirt.Example.Api.Constants;
using RedShirt.Example.Api.Core.UseCases.ExampleItem.Commands.Create;
using RedShirt.Example.Api.Core.UseCases.ExampleItem.Commands.Delete;
using RedShirt.Example.Api.Core.UseCases.ExampleItem.Models;
using RedShirt.Example.Api.Core.UseCases.ExampleItem.Queries.GetRecord;
using RedShirt.Example.Api.Core.UseCases.ExampleItem.Queries.ListRecords;
using RedShirt.Example.Api.Models.ExampleItem;

namespace RedShirt.Example.Api.Controllers;

[ApiController]
[EnableRateLimiting(RateLimitingConstants.PolicyHeaderExample)]
[Route("example")]
[ProducesJson]
public class ExampleItemController : ControllerBase
{
    [HttpDelete("{name}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute]
        string name,
        [FromServices]
        IDeleteExampleItemCommandHandler deleteExampleItemCommandHandler,
        CancellationToken cancellationToken)
    {
        await deleteExampleItemCommandHandler.Handle(new DeleteExampleItemCommand(name), cancellationToken);
        return Ok();
    }

    [HttpGet("{name}")]
    [ApproveReadOnly]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ExampleItemModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        [FromRoute]
        string name,
        [FromServices]
        IGetExampleItemRecordQueryHandler getExampleItemRecordQueryHandler,
        CancellationToken cancellationToken)
    {
        var model = await getExampleItemRecordQueryHandler.Handle(new GetExampleItemRecordQuery(name),
            cancellationToken);
        return Ok(model);
    }

    [HttpGet]
    [ApproveReadOnly]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ExampleItemListModel))]
    public async Task<IActionResult> GetList(
        [FromQuery]
        string? continuationToken,
        [FromServices]
        IListExampleItemRecordsQueryHandler listExampleItemRecordsQueryHandler,
        CancellationToken cancellationToken)
    {
        var model = await listExampleItemRecordsQueryHandler.Handle(
            new ListExampleItemRecordsQuery(continuationToken), cancellationToken);
        return Ok(model);
    }

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ExampleItemModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Put(
        [FromBody]
        ExampleItemPutRequest request,
        [FromHeader(Name = EndpointConstants.IdempotencyKeyHeader)]
        string idempotencyKey,
        [FromServices]
        ICreateExampleItemCommandHandler createExampleItemCommandHandler,
        CancellationToken cancellationToken)
    {
        var model = await createExampleItemCommandHandler.Handle(new CreateExampleItemCommand(
                new ExampleItemModel {Name = request.Name},
                string.IsNullOrWhiteSpace(idempotencyKey) ? Guid.NewGuid().ToString() : idempotencyKey),
            cancellationToken);

        return Ok(model);
    }
}