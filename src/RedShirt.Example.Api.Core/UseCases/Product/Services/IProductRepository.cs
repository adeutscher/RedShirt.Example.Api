using RedShirt.Example.Api.Core.UseCases.Product.Models;

namespace RedShirt.Example.Api.Core.UseCases.Product.Services;

public interface IProductRepository
{
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ProductModel?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ProductListModel> SearchAsync(ProductSearchParameters parameters, Guid? continuationToken,
        CancellationToken cancellationToken = default);

    Task<ProductModel> UpsertAsync(ProductModel item, CancellationToken cancellationToken = default);
}
