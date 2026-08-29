namespace RedShirt.Example.Api.Core.UseCases.Product.Queries.SearchRecords;

public record SearchProductRecordsQuery(
    int PageSize,
    DateTime? CreatedBeforeUtc,
    DateTime? CreatedAfterUtc,
    DateTime? UpdatedBeforeUtc,
    DateTime? UpdatedAfterUtc,
    string? Sku,
    string? SkuContains,
    string? Name,
    string? NameContains,
    string? Price,
    string? PriceGreaterThan,
    string? PriceLessThan,
    Guid? ContinuationToken);