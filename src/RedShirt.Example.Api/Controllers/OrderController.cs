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
[EnableRateLimiting(policyName: RateLimitingConstants.PolicyHeaderDefault)]
[Route("orders")]
[ProducesJson]
public class OrderController : ControllerBase
{
    [HttpDelete("{id:guid}")]
    [AuthorizeOrderWrite]
    [ProducesResponseType(statusCode: StatusCodes.Status200OK)]
    [ProducesResponseType(statusCode: StatusCodes.Status400BadRequest)]
    [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
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
        var existing =
            await getOrderRecordQueryHandler.Handle(new GetOrderRecordQuery(Id: id),
                cancellationToken: cancellationToken);
        await customerScopedResourceEnforcer.EnsureCanAccessAsync(user: User, customerId: existing.CustomerId);
        await deleteOrderCommandHandler.Handle(new DeleteOrderCommand(Id: id), cancellationToken: cancellationToken);
        return Ok();
    }

    [HttpGet("{id:guid}")]
    [ApproveOrderReadOnly]
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, Type = typeof(OrderDto))]
    [ProducesResponseType(statusCode: StatusCodes.Status400BadRequest)]
    [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        [FromRoute]
        Guid id,
        [FromServices]
        IGetOrderRecordQueryHandler getOrderRecordQueryHandler,
        [FromServices]
        ICustomerScopedResourceEnforcer customerScopedResourceEnforcer,
        CancellationToken cancellationToken)
    {
        var model = await getOrderRecordQueryHandler.Handle(new GetOrderRecordQuery(Id: id),
            cancellationToken: cancellationToken);
        await customerScopedResourceEnforcer.EnsureCanAccessAsync(user: User, customerId: model.CustomerId);
        return Ok(value: model);
    }

    [HttpPatch("{id:guid}")]
    [AuthorizeOrderWrite]
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, Type = typeof(OrderDto))]
    [ProducesResponseType(statusCode: StatusCodes.Status400BadRequest)]
    [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
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
        var existing =
            await getOrderRecordQueryHandler.Handle(new GetOrderRecordQuery(Id: id),
                cancellationToken: cancellationToken);
        await customerScopedResourceEnforcer.EnsureCanAccessAsync(user: User, customerId: existing.CustomerId);
        var model = await patchOrderCommandHandler.Handle(
            new PatchOrderCommand(
                Id: id,
                CustomerId: request.CustomerId,
                Status: request.Status,
                TotalAmount: request.TotalAmount,
                TotalPrice: request.TotalPrice,
                ClearTotalPrice: request.ClearTotalPrice),
            cancellationToken: cancellationToken);
        return Ok(value: model);
    }

    [HttpPost]
    [AuthorizeOrderWrite]
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, Type = typeof(OrderDto))]
    [ProducesResponseType(statusCode: StatusCodes.Status400BadRequest)]
    [ProducesResponseType(statusCode: StatusCodes.Status409Conflict)]
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
                CustomerId: request.CustomerId,
                Status: request.Status,
                TotalAmount: request.TotalAmount,
                TotalPrice: request.TotalPrice,
                string.IsNullOrWhiteSpace(value: idempotencyKey) ? Guid.NewGuid().ToString() : idempotencyKey),
            cancellationToken: cancellationToken);

        return Ok(value: model);
    }

    [HttpPut("{id:guid}")]
    [AuthorizeOrderWrite]
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, Type = typeof(OrderDto))]
    [ProducesResponseType(statusCode: StatusCodes.Status400BadRequest)]
    [ProducesResponseType(statusCode: StatusCodes.Status404NotFound)]
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
        var existing =
            await getOrderRecordQueryHandler.Handle(new GetOrderRecordQuery(Id: id),
                cancellationToken: cancellationToken);
        await customerScopedResourceEnforcer.EnsureCanAccessAsync(user: User, customerId: existing.CustomerId);
        var model = await updateOrderCommandHandler.Handle(
            new UpdateOrderCommand(
                Id: id,
                CustomerId: request.CustomerId,
                Status: request.Status,
                TotalAmount: request.TotalAmount,
                TotalPrice: request.TotalPrice),
            cancellationToken: cancellationToken);
        return Ok(value: model);
    }

    [HttpGet]
    [ApproveOrderReadOnly]
    [ProducesResponseType(statusCode: StatusCodes.Status200OK, Type = typeof(OrderSearchResponse))]
    public async Task<IActionResult> Search(
        [FromQuery]
        OrderSearchRequest request,
        [FromServices]
        ICustomerScopedResourceEnforcer customerScopedResourceEnforcer,
        [FromServices]
        ISearchOrderRecordsQueryHandler searchOrderRecordsQueryHandler,
        CancellationToken cancellationToken)
    {
        var customerId =
            customerScopedResourceEnforcer.ConstrainSearchCustomerId(user: User,
                requestedCustomerId: request.CustomerId);
        var model = await searchOrderRecordsQueryHandler.Handle(
            new SearchOrderRecordsQuery(
                PageSize: request.PageSize,
                CreatedBeforeUtc: request.CreatedBeforeUtc,
                CreatedAfterUtc: request.CreatedAfterUtc,
                UpdatedBeforeUtc: request.UpdatedBeforeUtc,
                UpdatedAfterUtc: request.UpdatedAfterUtc,
                CustomerId: customerId,
                Status: request.Status,
                StatusContains: request.StatusContains,
                TotalAmount: request.TotalAmount,
                TotalAmountGreaterThan: request.TotalAmountGreaterThan,
                TotalAmountLessThan: request.TotalAmountLessThan,
                TotalPrice: request.TotalPrice,
                TotalPriceGreaterThan: request.TotalPriceGreaterThan,
                TotalPriceLessThan: request.TotalPriceLessThan,
                TotalPriceIsNull: request.TotalPriceIsNull,
                ContinuationToken: request.ContinuationToken),
            cancellationToken: cancellationToken);
        return Ok(value: model);
    }
}