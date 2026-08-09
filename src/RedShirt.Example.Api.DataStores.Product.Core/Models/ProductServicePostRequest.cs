namespace RedShirt.Example.Api.DataStores.Product.Core.Models;

public class ProductServicePostRequest
{
    public required string Sku { get; init; }
    public required string Name { get; init; }
    public required string Price { get; init; }
}
