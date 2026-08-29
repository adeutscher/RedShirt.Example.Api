using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.DataStores.Product.Core.Models;
using RedShirt.Example.Api.DataStores.Product.Core.Services;
using System.Globalization;

namespace RedShirt.Example.Api.Core.UseCases.Product.Queries.SearchRecords;

public interface ISearchProductRecordsQueryHandler : ICqrsHandler<SearchProductRecordsQuery, ProductSearchResponse>;

internal class SearchProductRecordsQueryHandler(IProductService productService)
    : ISearchProductRecordsQueryHandler
{
    private static decimal? ParseOptionalDecimal(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : decimal.Parse(value, CultureInfo.InvariantCulture);
    }

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
            Price = ParseOptionalDecimal(query.Price),
            PriceGreaterThan = ParseOptionalDecimal(query.PriceGreaterThan),
            PriceLessThan = ParseOptionalDecimal(query.PriceLessThan)
        }, query.ContinuationToken, cancellationToken);

        return new ProductSearchResponse
        {
            ContinuationToken = result.ContinuationToken,
            // ReSharper disable once UseCollectionExpression
            Records = result.Records.Select(record => record.ToDto()).ToList()
        };
    }
}