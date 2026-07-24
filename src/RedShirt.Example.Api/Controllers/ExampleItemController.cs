using Microsoft.AspNetCore.Mvc;
using RedShirt.Example.Api.Attributes;
using RedShirt.Example.Api.Core.Models;
using RedShirt.Example.Api.Core.Services.Topics.ExampleItem;
using RedShirt.Example.Api.Models.ExampleItem;

namespace RedShirt.Example.Api.Controllers;

[ApiController]
[Route("example")]
[ProducesJson]
public class ExampleItemController(IExampleItemService exampleItemService) : ControllerBase
{
    [HttpDelete("{name}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] string name)
    {
        await exampleItemService.DeleteAsync(name);
        return Ok();
    }

    [HttpGet("{name}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ExampleItemModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get([FromRoute] string name)
    {
        var model = await exampleItemService.GetAsync(name);
        return Ok(model);
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ExampleItemListModel))]
    public async Task<IActionResult> GetList([FromQuery] string? continuationToken)
    {
        var model = await exampleItemService.GetListAsync(continuationToken);
        return Ok(model);
    }

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ExampleItemModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Put([FromBody] ExampleItemPutRequest request,
        [FromHeader(Name = "Idempotency-Key")]
        string idempotencyKey)
    {
        var model = await exampleItemService.PutAsync(new ExampleItemModel
        {
            Name = request.Name
        }, string.IsNullOrWhiteSpace(idempotencyKey) ? Guid.NewGuid().ToString() : idempotencyKey);

        return Ok(model);
    }
}