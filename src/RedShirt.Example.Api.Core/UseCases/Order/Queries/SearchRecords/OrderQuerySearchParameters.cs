namespace RedShirt.Example.Api.Core.UseCases.Order.Queries.SearchRecords;

public record OrderQuerySearchParameters(
    int PageSize,
    DateTime? CreatedBeforeUtc,
    DateTime? CreatedAfterUtc,
    DateTime? UpdatedBeforeUtc,
    DateTime? UpdatedAfterUtc,
    Guid? CustomerId,
    string? Status,
    string? StatusContains,
    string? TotalAmount,
    string? TotalAmountGreaterThan,
    string? TotalAmountLessThan,
    string? TotalPrice,
    string? TotalPriceGreaterThan,
    string? TotalPriceLessThan,
    bool TotalPriceIsNull);
