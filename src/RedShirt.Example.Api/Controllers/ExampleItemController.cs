using Microsoft.AspNetCore.Mvc;
using RedShirt.Example.Api.Attributes;
using RedShirt.Example.Api.Core.Exceptions;
using RedShirt.Example.Api.Core.Models;
using RedShirt.Example.Api.Core.Services;
using RedShirt.Example.Api.Models.ExampleItem;

namespace RedShirt.Example.Api.Controllers;

[ApiController]
[Route("example")]
[ProducesJson]
public class ExampleItemController(IExampleItemService exampleItemService) : Controller
{
    [HttpDelete("{name}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete([FromRoute] string name)
    {
        try
        {
            await exampleItemService.DeleteAsync(name);
        }
        catch (BadRequestException e)
        {
            return BadRequest(e.Message);
        }
        catch (ResourceNotFoundException)
        {
            return NotFound();
        }

        return Ok();
    }

    [HttpGet("{name}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ExampleItemModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get([FromRoute] string name)
    {
        try
        {
            var model = await exampleItemService.GetAsync(name);
            return Ok(model);
        }
        catch (BadRequestException e)
        {
            return BadRequest(e.Message);
        }
        catch (ResourceNotFoundException)
        {
            return NotFound();
        }
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
    public async Task<IActionResult> Put([FromBody] ExampleItemPutRequest request)
    {
        try
        {
            var model = await exampleItemService.PutAsync(new ExampleItemModel
            {
                Name = request.Name
            });

            return Ok(model);
        }
        catch (BadRequestException e)
        {
            return BadRequest(e.Message);
        }
    }
}