using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.DataStores.Order.Models.Generated;

namespace RedShirt.Example.Api.Core.UseCases.Order.Queries.SearchRecords;

public interface ISearchOrderRecordsQueryHandler : ICqrsHandler<SearchOrderRecordsQuery, OrderSearchResponse>;

internal class SearchOrderRecordsQueryHandler(IOrderService orderService)
    : ISearchOrderRecordsQueryHandler
{
    public async Task<OrderSearchResponse> Handle(SearchOrderRecordsQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = await orderService.SearchAsync(query.Parameters, query.ContinuationToken, cancellationToken);
        return new OrderSearchResponse
        {
            ContinuationToken = result.ContinuationToken,
            // ReSharper disable once UseCollectionExpression
            Records = result.Records.Select(record => record.ToDto()).ToList()
        };
    }
}