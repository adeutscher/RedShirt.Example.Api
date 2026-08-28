namespace RedShirt.Example.Api.DataStores.Product.Core.Models;

public class ProductDto
{
    public required Guid Id { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required DateTime UpdatedAtUtc { get; init; }
    public required string Sku { get; init; }
    public required string Name { get; init; }
    public required string Price { get; init; }
}