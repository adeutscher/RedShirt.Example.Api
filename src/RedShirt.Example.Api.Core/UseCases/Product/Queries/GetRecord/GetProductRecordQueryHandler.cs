using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.DataStores.Product.Core.Models;
using RedShirt.Example.Api.DataStores.Product.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.Product.Queries.GetRecord;

public interface IGetProductRecordQueryHandler : ICqrsHandler<GetProductRecordQuery, ProductDto>;

internal class GetProductRecordQueryHandler(
    IProductService productService,
    ICoreRequestValidator coreRequestValidator)
    : IGetProductRecordQueryHandler
{
    public async Task<ProductDto> Handle(GetProductRecordQuery query,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(query, cancellationToken);
        return await productService.GetByIdAsync(query.Id, cancellationToken);
    }
}
