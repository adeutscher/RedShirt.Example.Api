using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RedShirt.Example.Api.Attributes;
using RedShirt.Example.Api.Attributes.Authorization;
using RedShirt.Example.Api.Authorization.ResourceScoping.Customer;
using RedShirt.Example.Api.Constants;
using RedShirt.Example.Api.Core.UseCases.Customer.Commands.Create;
using RedShirt.Example.Api.Core.UseCases.Customer.Commands.Delete;
using RedShirt.Example.Api.Core.UseCases.Customer.Commands.Patch;
using RedShirt.Example.Api.Core.UseCases.Customer.Commands.Update;
using RedShirt.Example.Api.Core.UseCases.Customer.Queries.GetRecord;
using RedShirt.Example.Api.Core.UseCases.Customer.Queries.SearchRecords;
using RedShirt.Example.Api.DataStores.Customer.Core.Models;
using RedShirt.Example.Api.Models.Customer;

namespace RedShirt.Example.Api.Controllers;

[ApiController]
[EnableRateLimiting(RateLimitingConstants.PolicyHeaderDefault)]
[Route("customers")]
[ProducesJson]
public class CustomerController : ControllerBase
{
    [HttpDelete("{id:guid}")]
    [AuthorizeCustomerWrite]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute]
        Guid id,
        [FromServices]
        IGetCustomerRecordQueryHandler getCustomerRecordQueryHandler,
        [FromServices]
        ICustomerScopedResourceEnforcer customerScopedResourceEnforcer,
        [FromServices]
        IDeleteCustomerCommandHandler deleteCustomerCommandHandler,
        CancellationToken cancellationToken)
    {
        var existing = await getCustomerRecordQueryHandler.Handle(new GetCustomerRecordQuery(id), cancellationToken);
        await customerScopedResourceEnforcer.EnsureCanAccessAsync(User, existing.Id);
        await deleteCustomerCommandHandler.Handle(new DeleteCustomerCommand(id), cancellationToken);
        return Ok();
    }

    [HttpGet("{id:guid}")]
    [ApproveCustomerReadOnly]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CustomerDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        [FromRoute]
        Guid id,
        [FromServices]
        IGetCustomerRecordQueryHandler getCustomerRecordQueryHandler,
        [FromServices]
        ICustomerScopedResourceEnforcer customerScopedResourceEnforcer,
        CancellationToken cancellationToken)
    {
        var model = await getCustomerRecordQueryHandler.Handle(new GetCustomerRecordQuery(id), cancellationToken);
        await customerScopedResourceEnforcer.EnsureCanAccessAsync(User, model.Id);
        return Ok(model);
    }

    [HttpPatch("{id:guid}")]
    [AuthorizeCustomerWrite]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CustomerDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Patch(
        [FromRoute]
        Guid id,
        [FromBody]
        CustomerPatchRequest request,
        [FromServices]
        IGetCustomerRecordQueryHandler getCustomerRecordQueryHandler,
        [FromServices]
        ICustomerScopedResourceEnforcer customerScopedResourceEnforcer,
        [FromServices]
        IPatchCustomerCommandHandler patchCustomerCommandHandler,
        CancellationToken cancellationToken)
    {
        var existing = await getCustomerRecordQueryHandler.Handle(new GetCustomerRecordQuery(id), cancellationToken);
        await customerScopedResourceEnforcer.EnsureCanAccessAsync(User, existing.Id);
        var model = await patchCustomerCommandHandler.Handle(
            new PatchCustomerCommand(id, request.Email, request.DisplayName),
            cancellationToken);
        return Ok(model);
    }

    [HttpPost]
    [AuthorizeCustomerWrite]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CustomerDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Post(
        [FromBody]
        CustomerPostRequest request,
        [FromHeader(Name = EndpointConstants.IdempotencyKeyHeader)]
        string idempotencyKey,
        [FromServices]
        ICreateCustomerCommandHandler createCustomerCommandHandler,
        CancellationToken cancellationToken)
    {
        var model = await createCustomerCommandHandler.Handle(
            new CreateCustomerCommand(
                request.Email,
                request.DisplayName,
                string.IsNullOrWhiteSpace(idempotencyKey) ? Guid.NewGuid().ToString() : idempotencyKey),
            cancellationToken);

        return Ok(model);
    }

    [HttpPut("{id:guid}")]
    [AuthorizeCustomerWrite]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CustomerDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Put(
        [FromRoute]
        Guid id,
        [FromBody]
        CustomerPutRequest request,
        [FromServices]
        ICustomerScopedResourceEnforcer customerScopedResourceEnforcer,
        [FromServices]
        IUpdateCustomerCommandHandler updateCustomerCommandHandler,
        CancellationToken cancellationToken)
    {
        await customerScopedResourceEnforcer.EnsureCanAccessAsync(User, id);
        var model = await updateCustomerCommandHandler.Handle(
            new UpdateCustomerCommand(id, request.Email, request.DisplayName),
            cancellationToken);
        return Ok(model);
    }

    [HttpGet]
    [ApproveCustomerReadOnly]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(CustomerSearchResponse))]
    public async Task<IActionResult> Search(
        [FromQuery]
        CustomerSearchRequest request,
        [FromServices]
        ICustomerScopedResourceEnforcer customerScopedResourceEnforcer,
        [FromServices]
        ISearchCustomerRecordsQueryHandler searchCustomerRecordsQueryHandler,
        CancellationToken cancellationToken)
    {
        var id = customerScopedResourceEnforcer.ConstrainSearchCustomerId(User, request.Id);
        var model = await searchCustomerRecordsQueryHandler.Handle(
            new SearchCustomerRecordsQuery(
                new CustomerServiceSearchRequest
                {
                    PageSize = request.PageSize,
                    CreatedBeforeUtc = request.CreatedBeforeUtc,
                    CreatedAfterUtc = request.CreatedAfterUtc,
                    UpdatedBeforeUtc = request.UpdatedBeforeUtc,
                    UpdatedAfterUtc = request.UpdatedAfterUtc,
                    Id = id,
                    Email = request.Email,
                    EmailContains = request.EmailContains,
                    DisplayName = request.DisplayName,
                    DisplayNameContains = request.DisplayNameContains
                },
                request.ContinuationToken),
            cancellationToken);
        return Ok(model);
    }
}
