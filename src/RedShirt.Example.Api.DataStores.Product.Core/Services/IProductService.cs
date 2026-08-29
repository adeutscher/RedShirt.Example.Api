using RedShirt.Example.Api.DataStores.Product.Core.Models;

namespace RedShirt.Example.Api.DataStores.Product.Core.Services;

public interface IProductService
{
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductInternalDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ProductInternalDto> PatchAsync(ProductServicePatchRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductInternalDto> PostAsync(ProductServicePostRequest request,
        CancellationToken cancellationToken = default);

    Task<ProductInternalDto> PutAsync(ProductServicePutRequest request, CancellationToken cancellationToken = default);

    Task<ProductServiceSearchResponse> SearchAsync(ProductServiceSearchRequest parameters, Guid? continuationToken,
        CancellationToken cancellationToken = default);
}