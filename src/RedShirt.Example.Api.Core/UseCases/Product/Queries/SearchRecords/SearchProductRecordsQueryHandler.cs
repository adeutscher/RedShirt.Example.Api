using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.DataStores.Product.Core.Models;
using RedShirt.Example.Api.DataStores.Product.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.Product.Queries.SearchRecords;

public interface ISearchProductRecordsQueryHandler : ICqrsHandler<SearchProductRecordsQuery, ProductSearchResponse>;

internal class SearchProductRecordsQueryHandler(IProductService productService)
    : ISearchProductRecordsQueryHandler
{
    public async Task<ProductSearchResponse> Handle(SearchProductRecordsQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = await productService.SearchAsync(new ProductServiceSearchRequest
        {
            PageSize = query.PageSize,
            CreatedBeforeUtc = query.CreatedBeforeUtc,
            CreatedAfterUtc = query.CreatedAfterUtc,
            UpdatedBeforeUtc = query.UpdatedBeforeUtc,
            UpdatedAfterUtc = query.UpdatedAfterUtc,
            Sku = query.Sku,
            SkuContains = query.SkuContains,
            Name = query.Name,
            NameContains = query.NameContains,
            Price = query.Price,
            PriceGreaterThan = query.PriceGreaterThan,
            PriceLessThan = query.PriceLessThan
        }, query.ContinuationToken, cancellationToken);

        return new ProductSearchResponse
        {
            ContinuationToken = result.ContinuationToken,
            Records = result.Records
        };
    }
}
