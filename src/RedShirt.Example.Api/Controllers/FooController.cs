using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RedShirt.Example.Api.Attributes;
using RedShirt.Example.Api.Connectors.Foo.Core.Models;
using RedShirt.Example.Api.Constants;
using RedShirt.Example.Api.Core.UseCases.Foo.Commands.Create;
using RedShirt.Example.Api.Core.UseCases.Foo.Queries.GetRecord;
using RedShirt.Example.Api.Models.Foo;

namespace RedShirt.Example.Api.Controllers;

[ApiController]
[EnableRateLimiting(RateLimitingConstants.PolicyHeaderDefault)]
[Route("foo")]
[ProducesJson]
public class FooController : ControllerBase
{
    [HttpGet("{id:int}")]
    [ApproveReadOnly]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetFooConnectorResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Get(
        [FromRoute]
        int id,
        [FromServices]
        IGetFooRecordQueryHandler getFooRecordQueryHandler,
        CancellationToken cancellationToken)
    {
        var model = await getFooRecordQueryHandler.Handle(new GetFooRecordQuery(id), cancellationToken);
        return Ok(model);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CreateFooConnectorResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Post(
        [FromBody]
        FooPostRequest request,
        [FromHeader(Name = EndpointConstants.IdempotencyKeyHeader)]
        string idempotencyKey,
        [FromServices]
        ICreateFooCommandHandler createFooCommandHandler,
        CancellationToken cancellationToken)
    {
        var model = await createFooCommandHandler.Handle(
            new CreateFooCommand(
                request.Name,
                string.IsNullOrWhiteSpace(idempotencyKey) ? Guid.NewGuid().ToString() : idempotencyKey),
            cancellationToken);

        return Ok(model);
    }
}