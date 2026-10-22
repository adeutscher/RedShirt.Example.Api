using RedShirt.Example.Api.DataStores.Analyzers.Abstractions.Attributes;

namespace RedShirt.Example.Api.Models.Product;

public class ProductPatchRequest
{
    public string? Sku { get; set; }
    public string? Name { get; set; }

    [StoredAsDecimal]
    public string? Price { get; set; }
}
