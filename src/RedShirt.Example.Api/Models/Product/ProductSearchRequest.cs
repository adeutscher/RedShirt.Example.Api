using RedShirt.Example.Api.DataStores.Analyzers.Abstractions.Attributes;

namespace RedShirt.Example.Api.Models.Product;

public class ProductSearchRequest
{
    public int PageSize { get; set; }
    public DateTime? CreatedBeforeUtc { get; set; }
    public DateTime? CreatedAfterUtc { get; set; }
    public DateTime? UpdatedBeforeUtc { get; set; }
    public DateTime? UpdatedAfterUtc { get; set; }
    public string? Sku { get; set; }
    public string? SkuContains { get; set; }
    public string? Name { get; set; }
    public string? NameContains { get; set; }

    [StoredAsDecimal]
    public string? Price { get; set; }

    [StoredAsDecimal]
    public string? PriceGreaterThan { get; set; }

    [StoredAsDecimal]
    public string? PriceLessThan { get; set; }

    public Guid? ContinuationToken { get; set; }
}
