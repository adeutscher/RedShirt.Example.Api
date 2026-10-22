namespace RedShirt.Example.Api.Core.UseCases.Order.Queries.SearchRecords;

public record SearchOrderRecordsQuery(
    int PageSize,
    DateTime? CreatedBeforeUtc,
    DateTime? CreatedAfterUtc,
    DateTime? UpdatedBeforeUtc,
    DateTime? UpdatedAfterUtc,
    Guid? CustomerId,
    string? Status,
    string? StatusContains,
    decimal? TotalAmount,
    decimal? TotalAmountGreaterThan,
    decimal? TotalAmountLessThan,
    decimal? TotalPrice,
    decimal? TotalPriceGreaterThan,
    decimal? TotalPriceLessThan,
    bool TotalPriceIsNull,
    Guid? ContinuationToken);