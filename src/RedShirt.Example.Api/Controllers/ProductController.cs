using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RedShirt.Example.Api.Attributes;
using RedShirt.Example.Api.Constants;
using RedShirt.Example.Api.Core.UseCases.Product.Commands.Create;
using RedShirt.Example.Api.Core.UseCases.Product.Commands.Delete;
using RedShirt.Example.Api.Core.UseCases.Product.Commands.Patch;
using RedShirt.Example.Api.Core.UseCases.Product.Commands.Update;
using RedShirt.Example.Api.Core.UseCases.Product.Models;
using RedShirt.Example.Api.Core.UseCases.Product.Queries.GetRecord;
using RedShirt.Example.Api.Core.UseCases.Product.Queries.SearchRecords;
using RedShirt.Example.Api.Models.Product;

namespace RedShirt.Example.Api.Controllers;

[ApiController]
[EnableRateLimiting(RateLimitingConstants.PolicyHeaderExample)]
[Route("products")]
[ProducesJson]
public class ProductController : ControllerBase
{
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute]
        Guid id,
        [FromServices]
        IDeleteProductCommandHandler deleteProductCommandHandler,
        CancellationToken cancellationToken)
    {
        await deleteProductCommandHandler.Handle(new DeleteProductCommand(id), cancellationToken);
        return Ok();
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ProductModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        [FromRoute]
        Guid id,
        [FromServices]
        IGetProductRecordQueryHandler getProductRecordQueryHandler,
        CancellationToken cancellationToken)
    {
        var model = await getProductRecordQueryHandler.Handle(new GetProductRecordQuery(id), cancellationToken);
        return Ok(model);
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ProductListModel))]
    public async Task<IActionResult> Search(
        [FromQuery]
        int pageSize,
        [FromQuery]
        DateTime? createdBeforeUtc,
        [FromQuery]
        DateTime? createdAfterUtc,
        [FromQuery]
        DateTime? updatedBeforeUtc,
        [FromQuery]
        DateTime? updatedAfterUtc,
        [FromQuery]
        string? sku,
        [FromQuery]
        string? skuContains,
        [FromQuery]
        string? name,
        [FromQuery]
        string? nameContains,
        [FromQuery]
        string? price,
        [FromQuery]
        string? priceGreaterThan,
        [FromQuery]
        string? priceLessThan,
        [FromQuery]
        Guid? continuationToken,
        [FromServices]
        ISearchProductRecordsQueryHandler searchProductRecordsQueryHandler,
        CancellationToken cancellationToken)
    {
        var model = await searchProductRecordsQueryHandler.Handle(
            new SearchProductRecordsQuery(
                new ProductSearchParameters
                {
                    PageSize = pageSize,
                    CreatedBeforeUtc = createdBeforeUtc,
                    CreatedAfterUtc = createdAfterUtc,
                    UpdatedBeforeUtc = updatedBeforeUtc,
                    UpdatedAfterUtc = updatedAfterUtc,
                    Sku = sku,
                    SkuContains = skuContains,
                    Name = name,
                    NameContains = nameContains,
                    Price = price,
                    PriceGreaterThan = priceGreaterThan,
                    PriceLessThan = priceLessThan
                },
                continuationToken),
            cancellationToken);
        return Ok(model);
    }

    [HttpPatch("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ProductModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Patch(
        [FromRoute]
        Guid id,
        [FromBody]
        ProductPatchRequest request,
        [FromServices]
        IPatchProductCommandHandler patchProductCommandHandler,
        CancellationToken cancellationToken)
    {
        var model = await patchProductCommandHandler.Handle(
            new PatchProductCommand(id, request.Sku, request.Name, request.Price),
            cancellationToken);
        return Ok(model);
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ProductModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Post(
        [FromBody]
        ProductPostRequest request,
        [FromHeader(Name = "Idempotency-Key")]
        string idempotencyKey,
        [FromServices]
        ICreateProductCommandHandler createProductCommandHandler,
        CancellationToken cancellationToken)
    {
        var model = await createProductCommandHandler.Handle(
            new CreateProductCommand(
                request.Sku,
                request.Name,
                request.Price,
                string.IsNullOrWhiteSpace(idempotencyKey) ? Guid.NewGuid().ToString() : idempotencyKey),
            cancellationToken);

        return Ok(model);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(ProductModel))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Put(
        [FromRoute]
        Guid id,
        [FromBody]
        ProductPutRequest request,
        [FromServices]
        IUpdateProductCommandHandler updateProductCommandHandler,
        CancellationToken cancellationToken)
    {
        var model = await updateProductCommandHandler.Handle(
            new UpdateProductCommand(id, request.Sku, request.Name, request.Price),
            cancellationToken);
        return Ok(model);
    }
}
