using RedShirt.Example.Api.DataStores.Product.Core.Models;

namespace RedShirt.Example.Api.DataStores.Product.Implementation.Repositories;

internal interface IProductRepository
{
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductInternalDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ProductServiceSearchResponse> SearchAsync(ProductServiceSearchRequest parameters, Guid? continuationToken,
        CancellationToken cancellationToken = default);

    Task<ProductInternalDto> UpsertAsync(ProductInternalDto item, CancellationToken cancellationToken = default);
}