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
        return await orderService.SearchAsync(new OrderServiceSearchRequest
        {
            PageSize = query.PageSize,
            CreatedBeforeUtc = query.CreatedBeforeUtc,
            CreatedAfterUtc = query.CreatedAfterUtc,
            UpdatedBeforeUtc = query.UpdatedBeforeUtc,
            UpdatedAfterUtc = query.UpdatedAfterUtc,
            CustomerId = query.CustomerId,
            Status = query.Status,
            StatusContains = query.StatusContains,
            TotalAmount = query.TotalAmount,
            TotalAmountGreaterThan = query.TotalAmountGreaterThan,
            TotalAmountLessThan = query.TotalAmountLessThan,
            TotalPrice = query.TotalPrice,
            TotalPriceGreaterThan = query.TotalPriceGreaterThan,
            TotalPriceLessThan = query.TotalPriceLessThan,
            TotalPriceIsNull = query.TotalPriceIsNull
        }, query.ContinuationToken, cancellationToken);
    }
}
