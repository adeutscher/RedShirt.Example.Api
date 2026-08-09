using RedShirt.Example.Api.DataStores.Product.Core.Models;

namespace RedShirt.Example.Api.DataStores.Product.Core.Services;

public interface IProductService
{
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductDto> PatchAsync(ProductServicePatchRequest request, CancellationToken cancellationToken = default);
    Task<ProductDto> PostAsync(ProductServicePostRequest request, CancellationToken cancellationToken = default);
    Task<ProductDto> PutAsync(ProductServicePutRequest request, CancellationToken cancellationToken = default);

    Task<ProductSearchResponse> SearchAsync(ProductServiceSearchRequest parameters, Guid? continuationToken,
        CancellationToken cancellationToken = default);
}