using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.DataStores.Order.Models.Generated;

namespace RedShirt.Example.Api.Core.UseCases.Order.Queries.SearchRecords;

public interface ISearchOrderRecordsQueryHandler : ICqrsHandler<SearchOrderRecordsQuery, OrderSearchResponse>;

internal class SearchOrderRecordsQueryHandler(IOrderService orderService)
    : ISearchOrderRecordsQueryHandler
{
    public Task<OrderSearchResponse> Handle(SearchOrderRecordsQuery query,
        CancellationToken cancellationToken = default)
    {
        return orderService.SearchAsync(query.Parameters, query.ContinuationToken, cancellationToken);
    }
}
