namespace RedShirt.Example.Api.Implementations.Products.Models;

public class ProductSearchResponse
{
    public required Guid? ContinuationToken { get; init; }
    public required List<ProductDto> Records { get; init; }
}
