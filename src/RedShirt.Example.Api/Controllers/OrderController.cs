using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RedShirt.Example.Api.Attributes;
using RedShirt.Example.Api.Constants;
using RedShirt.Example.Api.Core.UseCases.Order.Commands.Create;
using RedShirt.Example.Api.Core.UseCases.Order.Commands.Delete;
using RedShirt.Example.Api.Core.UseCases.Order.Commands.Patch;
using RedShirt.Example.Api.Core.UseCases.Order.Commands.Update;
using RedShirt.Example.Api.Core.UseCases.Order.Queries.GetRecord;
using RedShirt.Example.Api.Core.UseCases.Order.Queries.SearchRecords;
using RedShirt.Example.Api.DataStores.Order.Models;
using RedShirt.Example.Api.DataStores.Order.Models.Generated;
using RedShirt.Example.Api.Models.Order;

namespace RedShirt.Example.Api.Controllers;

[ApiController]
[EnableRateLimiting(RateLimitingConstants.PolicyHeaderDefault)]
[Route("orders")]
[ProducesJson]
public class OrderController : ControllerBase
{
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute]
        Guid id,
        [FromServices]
        IDeleteOrderCommandHandler deleteOrderCommandHandler,
        CancellationToken cancellationToken)
    {
        await deleteOrderCommandHandler.Handle(new DeleteOrderCommand(id), cancellationToken);
        return Ok();
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OrderDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        [FromRoute]
        Guid id,
        [FromServices]
        IGetOrderRecordQueryHandler getOrderRecordQueryHandler,
        CancellationToken cancellationToken)
    {
        var model = await getOrderRecordQueryHandler.Handle(new GetOrderRecordQuery(id), cancellationToken);
        return Ok(model);
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OrderDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Patch(
        [FromRoute]
        Guid id,
        [FromBody]
        OrderPatchRequest request,
        [FromServices]
        IPatchOrderCommandHandler patchOrderCommandHandler,
        CancellationToken cancellationToken)
    {
        var model = await patchOrderCommandHandler.Handle(
            new PatchOrderCommand(
                id,
                request.CustomerId,
                request.Status,
                request.TotalAmount,
                request.TotalPrice,
                request.ClearTotalPrice),
            cancellationToken);
        return Ok(model);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OrderDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Post(
        [FromBody]
        OrderPostRequest request,
        [FromHeader(Name = "Idempotency-Key")]
        string idempotencyKey,
        [FromServices]
        ICreateOrderCommandHandler createOrderCommandHandler,
        CancellationToken cancellationToken)
    {
        var model = await createOrderCommandHandler.Handle(
            new CreateOrderCommand(
                request.CustomerId,
                request.Status,
                request.TotalAmount,
                request.TotalPrice,
                string.IsNullOrWhiteSpace(idempotencyKey) ? Guid.NewGuid().ToString() : idempotencyKey),
            cancellationToken);

        return Ok(model);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OrderDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Put(
        [FromRoute]
        Guid id,
        [FromBody]
        OrderPutRequest request,
        [FromServices]
        IUpdateOrderCommandHandler updateOrderCommandHandler,
        CancellationToken cancellationToken)
    {
        var model = await updateOrderCommandHandler.Handle(
            new UpdateOrderCommand(
                id,
                request.CustomerId,
                request.Status,
                request.TotalAmount,
                request.TotalPrice),
            cancellationToken);
        return Ok(model);
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OrderSearchResponse))]
    public async Task<IActionResult> Search(
        [FromQuery]
        OrderSearchRequest request,
        [FromServices]
        ISearchOrderRecordsQueryHandler searchOrderRecordsQueryHandler,
        CancellationToken cancellationToken)
    {
        var model = await searchOrderRecordsQueryHandler.Handle(
            new SearchOrderRecordsQuery(
                new OrderServiceSearchRequest
                {
                    PageSize = request.PageSize,
                    CreatedBeforeUtc = request.CreatedBeforeUtc,
                    CreatedAfterUtc = request.CreatedAfterUtc,
                    UpdatedBeforeUtc = request.UpdatedBeforeUtc,
                    UpdatedAfterUtc = request.UpdatedAfterUtc,
                    CustomerId = request.CustomerId,
                    Status = request.Status,
                    StatusContains = request.StatusContains,
                    TotalAmount = request.TotalAmount,
                    TotalAmountGreaterThan = request.TotalAmountGreaterThan,
                    TotalAmountLessThan = request.TotalAmountLessThan,
                    TotalPrice = request.TotalPrice,
                    TotalPriceGreaterThan = request.TotalPriceGreaterThan,
                    TotalPriceLessThan = request.TotalPriceLessThan,
                    TotalPriceIsNull = request.TotalPriceIsNull
                },
                request.ContinuationToken),
            cancellationToken);
        return Ok(model);
    }
}