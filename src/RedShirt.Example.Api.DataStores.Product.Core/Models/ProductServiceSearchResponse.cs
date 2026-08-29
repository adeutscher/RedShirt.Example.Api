namespace RedShirt.Example.Api.DataStores.Product.Core.Models;

public class ProductServiceSearchResponse
{
    public required Guid? ContinuationToken { get; init; }
    public required List<ProductInternalDto> Records { get; init; }
}