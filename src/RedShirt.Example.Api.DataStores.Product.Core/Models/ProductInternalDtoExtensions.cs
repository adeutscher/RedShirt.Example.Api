using System.Globalization;

namespace RedShirt.Example.Api.DataStores.Product.Core.Models;

public static class ProductInternalDtoExtensions
{
    public static ProductDto ToDto(this ProductInternalDto source)
    {
        return new ProductDto
        {
            Id = source.Id,
            CreatedAtUtc = source.CreatedAtUtc,
            UpdatedAtUtc = source.UpdatedAtUtc,
            Sku = source.Sku,
            Name = source.Name,
            Price = source.Price.ToString(CultureInfo.InvariantCulture)
        };
    }
}