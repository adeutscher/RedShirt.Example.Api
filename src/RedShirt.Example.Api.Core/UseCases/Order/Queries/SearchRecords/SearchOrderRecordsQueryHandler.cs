using RedShirt.Example.Api.Core.Cqrs;
using System.Globalization;

namespace RedShirt.Example.Api.Core.UseCases.Order.Queries.SearchRecords;

public interface ISearchOrderRecordsQueryHandler : ICqrsHandler<SearchOrderRecordsQuery, OrderSearchResponse>;

internal class SearchOrderRecordsQueryHandler(IOrderService orderService)
    : ISearchOrderRecordsQueryHandler
{
    private static decimal? ParseOptionalDecimal(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : decimal.Parse(value, CultureInfo.InvariantCulture);
    }

    public async Task<OrderSearchResponse> Handle(SearchOrderRecordsQuery query,
        CancellationToken cancellationToken = default)
    {
        var result = await orderService.SearchAsync(new OrderServiceSearchRequest
        {
            PageSize = query.PageSize,
            CreatedBeforeUtc = query.CreatedBeforeUtc,
            CreatedAfterUtc = query.CreatedAfterUtc,
            UpdatedBeforeUtc = query.UpdatedBeforeUtc,
            UpdatedAfterUtc = query.UpdatedAfterUtc,
            CustomerId = query.CustomerId,
            Status = query.Status,
            StatusContains = query.StatusContains,
            TotalAmount = ParseOptionalDecimal(query.TotalAmount),
            TotalAmountGreaterThan = ParseOptionalDecimal(query.TotalAmountGreaterThan),
            TotalAmountLessThan = ParseOptionalDecimal(query.TotalAmountLessThan),
            TotalPrice = ParseOptionalDecimal(query.TotalPrice),
            TotalPriceGreaterThan = ParseOptionalDecimal(query.TotalPriceGreaterThan),
            TotalPriceLessThan = ParseOptionalDecimal(query.TotalPriceLessThan),
            TotalPriceIsNull = query.TotalPriceIsNull
        }, query.ContinuationToken, cancellationToken);

        return new OrderSearchResponse
        {
            ContinuationToken = result.ContinuationToken,
            Records = result.Records.Select(record => record.ToDto()).ToList()
        };
    }
}