namespace RedShirt.Example.Api.Implementations.Products.Services;

internal static class SupportingExtensions
{
    public static bool IsTheSameAs(this Models.ProductDto a, Models.ProductDto b)
    {
        return a.Sku == b.Sku && a.Name == b.Name && a.Price == b.Price;
    }

    public static bool AreChangesRequested(this Models.ProductServicePatchRequest subject)
    {
        return !string.IsNullOrWhiteSpace(subject.Sku)
               || !string.IsNullOrWhiteSpace(subject.Name)
               || subject.Price.HasValue;
    }
}
