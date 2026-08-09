namespace RedShirt.Example.Api.DataStores.Product.Core.Models;

public class ProductServiceSearchRequest
{
    public required int PageSize { get; init; }
    public required DateTime? CreatedBeforeUtc { get; init; }
    public required DateTime? CreatedAfterUtc { get; init; }
    public required DateTime? UpdatedBeforeUtc { get; init; }
    public required DateTime? UpdatedAfterUtc { get; init; }
    public required string? Sku { get; init; }
    public required string? SkuContains { get; init; }
    public required string? Name { get; init; }
    public required string? NameContains { get; init; }
    public required string? Price { get; init; }
    public required string? PriceGreaterThan { get; init; }
    public required string? PriceLessThan { get; init; }
}