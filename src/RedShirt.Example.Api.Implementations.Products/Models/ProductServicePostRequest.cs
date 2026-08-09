namespace RedShirt.Example.Api.Implementations.Products.Models;

public class ProductServicePostRequest
{
    public required string Sku { get; init; }
    public required string Name { get; init; }
    public required string Price { get; init; }
}