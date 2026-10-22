namespace RedShirt.Example.Api.Models.Product;

public class ProductPatchRequest
{
    public string? Sku { get; set; }
    public string? Name { get; set; }
    public decimal? Price { get; set; }
}