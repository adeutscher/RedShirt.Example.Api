using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RedShirt.Example.Api.Attributes;
using RedShirt.Example.Api.Attributes.Authorization;
using RedShirt.Example.Api.Authorization.ResourceScoping.Customer;
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
    [AuthorizeOrderWrite]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute]
        Guid id,
        [FromServices]
        IGetOrderRecordQueryHandler getOrderRecordQueryHandler,
        [FromServices]
        ICustomerScopedResourceEnforcer customerScopedResourceEnforcer,
        [FromServices]
        IDeleteOrderCommandHandler deleteOrderCommandHandler,
        CancellationToken cancellationToken)
    {
        var existing = await getOrderRecordQueryHandler.Handle(new GetOrderRecordQuery(id), cancellationToken);
        await customerScopedResourceEnforcer.EnsureCanAccessAsync(User, existing.CustomerId);
        await deleteOrderCommandHandler.Handle(new DeleteOrderCommand(id), cancellationToken);
        return Ok();
    }

    [HttpGet("{id:guid}")]
    [ApproveOrderReadOnly]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OrderDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        [FromRoute]
        Guid id,
        [FromServices]
        IGetOrderRecordQueryHandler getOrderRecordQueryHandler,
        [FromServices]
        ICustomerScopedResourceEnforcer customerScopedResourceEnforcer,
        CancellationToken cancellationToken)
    {
        var model = await getOrderRecordQueryHandler.Handle(new GetOrderRecordQuery(id), cancellationToken);
        await customerScopedResourceEnforcer.EnsureCanAccessAsync(User, model.CustomerId);
        return Ok(model);
    }

    [HttpPatch("{id:guid}")]
    [AuthorizeOrderWrite]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OrderDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Patch(
        [FromRoute]
        Guid id,
        [FromBody]
        OrderPatchRequest request,
        [FromServices]
        IGetOrderRecordQueryHandler getOrderRecordQueryHandler,
        [FromServices]
        ICustomerScopedResourceEnforcer customerScopedResourceEnforcer,
        [FromServices]
        IPatchOrderCommandHandler patchOrderCommandHandler,
        CancellationToken cancellationToken)
    {
        var existing = await getOrderRecordQueryHandler.Handle(new GetOrderRecordQuery(id), cancellationToken);
        await customerScopedResourceEnforcer.EnsureCanAccessAsync(User, existing.CustomerId);
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
    [AuthorizeOrderWrite]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OrderDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Post(
        [FromBody]
        OrderPostRequest request,
        [FromHeader(Name = EndpointConstants.IdempotencyKeyHeader)]
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
    [AuthorizeOrderWrite]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OrderDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Put(
        [FromRoute]
        Guid id,
        [FromBody]
        OrderPutRequest request,
        [FromServices]
        IGetOrderRecordQueryHandler getOrderRecordQueryHandler,
        [FromServices]
        ICustomerScopedResourceEnforcer customerScopedResourceEnforcer,
        [FromServices]
        IUpdateOrderCommandHandler updateOrderCommandHandler,
        CancellationToken cancellationToken)
    {
        var existing = await getOrderRecordQueryHandler.Handle(new GetOrderRecordQuery(id), cancellationToken);
        await customerScopedResourceEnforcer.EnsureCanAccessAsync(User, existing.CustomerId);
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
    [ApproveOrderReadOnly]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(OrderSearchResponse))]
    public async Task<IActionResult> Search(
        [FromQuery]
        OrderSearchRequest request,
        [FromServices]
        ICustomerScopedResourceEnforcer customerScopedResourceEnforcer,
        [FromServices]
        ISearchOrderRecordsQueryHandler searchOrderRecordsQueryHandler,
        CancellationToken cancellationToken)
    {
        var customerId = customerScopedResourceEnforcer.ConstrainSearchCustomerId(User, request.CustomerId);
        var model = await searchOrderRecordsQueryHandler.Handle(
            new SearchOrderRecordsQuery(
                new OrderQuerySearchParameters(
                    request.PageSize,
                    request.CreatedBeforeUtc,
                    request.CreatedAfterUtc,
                    request.UpdatedBeforeUtc,
                    request.UpdatedAfterUtc,
                    customerId,
                    request.Status,
                    request.StatusContains,
                    request.TotalAmount,
                    request.TotalAmountGreaterThan,
                    request.TotalAmountLessThan,
                    request.TotalPrice,
                    request.TotalPriceGreaterThan,
                    request.TotalPriceLessThan,
                    request.TotalPriceIsNull),
                request.ContinuationToken),
            cancellationToken);
        return Ok(model);
    }
}