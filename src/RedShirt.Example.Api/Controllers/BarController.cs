using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RedShirt.Example.Api.Attributes;
using RedShirt.Example.Api.Connectors.Bar.Core.Models;
using RedShirt.Example.Api.Constants;
using RedShirt.Example.Api.Core.UseCases.Bar.Commands.Create;
using RedShirt.Example.Api.Core.UseCases.Bar.Queries.GetRecord;
using RedShirt.Example.Api.Models.Bar;

namespace RedShirt.Example.Api.Controllers;

[ApiController]
[EnableRateLimiting(RateLimitingConstants.PolicyHeaderDefault)]
[Route("bar")]
[ProducesJson]
public class BarController : ControllerBase
{
    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(GetBarConnectorResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Get(
        [FromRoute]
        int id,
        [FromServices]
        IGetBarRecordQueryHandler getBarRecordQueryHandler,
        CancellationToken cancellationToken)
    {
        var model = await getBarRecordQueryHandler.Handle(new GetBarRecordQuery(id), cancellationToken);
        return Ok(model);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CreateBarConnectorResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Post(
        [FromBody]
        BarPostRequest request,
        [FromHeader(Name = EndpointConstants.IdempotencyKeyHeader)]
        string idempotencyKey,
        [FromServices]
        ICreateBarCommandHandler createBarCommandHandler,
        CancellationToken cancellationToken)
    {
        var model = await createBarCommandHandler.Handle(
            new CreateBarCommand(
                request.Name,
                string.IsNullOrWhiteSpace(idempotencyKey) ? Guid.NewGuid().ToString() : idempotencyKey),
            cancellationToken);

        return Ok(model);
    }
}