using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.DataStores.Order.Models.Generated;
using System.Globalization;

namespace RedShirt.Example.Api.Core.UseCases.Order.Queries.SearchRecords;

public interface ISearchOrderRecordsQueryHandler : ICqrsHandler<SearchOrderRecordsQuery, OrderSearchResponse>;

internal class SearchOrderRecordsQueryHandler(IOrderService orderService)
    : ISearchOrderRecordsQueryHandler
{
    public async Task<OrderSearchResponse> Handle(SearchOrderRecordsQuery query,
        CancellationToken cancellationToken = default)
    {
        var parameters = query.Parameters;
        var result = await orderService.SearchAsync(new OrderServiceSearchRequest
        {
            PageSize = parameters.PageSize,
            CreatedBeforeUtc = parameters.CreatedBeforeUtc,
            CreatedAfterUtc = parameters.CreatedAfterUtc,
            UpdatedBeforeUtc = parameters.UpdatedBeforeUtc,
            UpdatedAfterUtc = parameters.UpdatedAfterUtc,
            CustomerId = parameters.CustomerId,
            Status = parameters.Status,
            StatusContains = parameters.StatusContains,
            TotalAmount = ParseOptionalDecimal(parameters.TotalAmount),
            TotalAmountGreaterThan = ParseOptionalDecimal(parameters.TotalAmountGreaterThan),
            TotalAmountLessThan = ParseOptionalDecimal(parameters.TotalAmountLessThan),
            TotalPrice = ParseOptionalDecimal(parameters.TotalPrice),
            TotalPriceGreaterThan = ParseOptionalDecimal(parameters.TotalPriceGreaterThan),
            TotalPriceLessThan = ParseOptionalDecimal(parameters.TotalPriceLessThan),
            TotalPriceIsNull = parameters.TotalPriceIsNull
        }, query.ContinuationToken, cancellationToken);

        return new OrderSearchResponse
        {
            ContinuationToken = result.ContinuationToken,
            Records = result.Records.Select(record => record.ToDto()).ToList()
        };
    }

    private static decimal? ParseOptionalDecimal(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : decimal.Parse(value, CultureInfo.InvariantCulture);
    }
}
