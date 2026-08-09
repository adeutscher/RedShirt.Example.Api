using RedShirt.Example.Api.Core.UseCases.Product.Models;

namespace RedShirt.Example.Api.Core.UseCases.Product.Services;

internal static class ProductModelExtensions
{
    public static bool AreChangesRequested(string? sku, string? name, string? price)
    {
        return !string.IsNullOrWhiteSpace(sku)
               || !string.IsNullOrWhiteSpace(name)
               || !string.IsNullOrWhiteSpace(price);
    }

    public static bool IsTheSameAs(this ProductModel a, ProductModel b)
    {
        return a.Sku == b.Sku && a.Name == b.Name && a.Price == b.Price;
    }
}
