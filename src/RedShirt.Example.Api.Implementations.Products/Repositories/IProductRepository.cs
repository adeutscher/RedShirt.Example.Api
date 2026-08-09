using RedShirt.Example.Api.Implementations.Products.Models;

namespace RedShirt.Example.Api.Implementations.Products.Repositories;

internal interface IProductRepository
{
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ProductSearchResponse> SearchAsync(ProductServiceSearchRequest parameters, Guid? continuationToken,
        CancellationToken cancellationToken = default);

    Task<ProductDto> UpsertAsync(ProductDto item, CancellationToken cancellationToken = default);
}
