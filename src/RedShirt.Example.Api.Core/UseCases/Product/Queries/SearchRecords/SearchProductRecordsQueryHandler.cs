using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.UseCases.Product.Models;
using RedShirt.Example.Api.Core.UseCases.Product.Services;

namespace RedShirt.Example.Api.Core.UseCases.Product.Queries.SearchRecords;

public interface ISearchProductRecordsQueryHandler : ICqrsHandler<SearchProductRecordsQuery, ProductListModel>;

internal class SearchProductRecordsQueryHandler(IProductRepository repository)
    : ISearchProductRecordsQueryHandler
{
    public Task<ProductListModel> Handle(SearchProductRecordsQuery query,
        CancellationToken cancellationToken = default)
    {
        return repository.SearchAsync(query.Parameters, query.ContinuationToken, cancellationToken);
    }
}
