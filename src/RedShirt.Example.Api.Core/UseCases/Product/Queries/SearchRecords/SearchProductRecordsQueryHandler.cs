using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.DataStores.Product.Core.Models;
using RedShirt.Example.Api.DataStores.Product.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.Product.Queries.SearchRecords;

public interface ISearchProductRecordsQueryHandler : ICqrsHandler<SearchProductRecordsQuery, ProductSearchResponse>;

internal class SearchProductRecordsQueryHandler(IProductService productService)
    : ISearchProductRecordsQueryHandler
{
    public Task<ProductSearchResponse> Handle(SearchProductRecordsQuery query,
        CancellationToken cancellationToken = default)
    {
        return productService.SearchAsync(query.Parameters, query.ContinuationToken, cancellationToken);
    }
}