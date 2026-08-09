namespace RedShirt.Example.Api.Core.UseCases.Product.Models;

public class ProductModel
{
    public required Guid Id { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required DateTime UpdatedAtUtc { get; init; }
    public required string Sku { get; init; }
    public required string Name { get; init; }
    public required string Price { get; init; }
}
