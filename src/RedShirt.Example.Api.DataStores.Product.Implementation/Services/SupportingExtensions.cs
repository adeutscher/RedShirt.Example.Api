using RedShirt.Example.Api.DataStores.Product.Core.Models;

namespace RedShirt.Example.Api.DataStores.Product.Implementation.Services;

internal static class SupportingExtensions
{
    public static bool AreChangesRequested(this ProductServicePatchRequest subject)
    {
        return !string.IsNullOrWhiteSpace(subject.Sku)
               || !string.IsNullOrWhiteSpace(subject.Name)
               || !string.IsNullOrWhiteSpace(subject.Price);
    }

    public static bool IsTheSameAs(this ProductDto a, ProductDto b)
    {
        return a.Sku == b.Sku && a.Name == b.Name && a.Price == b.Price;
    }
}