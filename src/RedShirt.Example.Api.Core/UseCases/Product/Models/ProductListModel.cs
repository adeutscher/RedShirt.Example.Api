namespace RedShirt.Example.Api.Core.UseCases.Product.Models;

public class ProductListModel
{
    public required Guid? ContinuationToken { get; init; }
    public required List<ProductModel> Items { get; init; }
}
