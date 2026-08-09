namespace RedShirt.Example.Api.Implementations.Products.Models;

public class ProductServicePutRequest
{
    public required Guid Id { get; init; }
    public required string Sku { get; init; }
    public required string Name { get; init; }
    public required decimal Price { get; init; }
}
