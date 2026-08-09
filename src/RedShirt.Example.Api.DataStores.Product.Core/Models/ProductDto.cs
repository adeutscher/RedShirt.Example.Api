using RedShirt.Example.Api.Common.Analyzers.Database.Abstractions.Attributes;
using RedShirt.Example.Api.Implementations.Constants;

namespace RedShirt.Example.Api.DataStores.Product.Core.Models;

[DbTable("Product", DatabaseConstants.PrimaryDatabaseConnectionStringName)]
public class ProductDto
{
    [DbKey]
    public required Guid Id { get; init; }

    [CreatedAtProperty]
    public required DateTime CreatedAtUtc { get; init; }

    [UpdatedAtProperty]
    public required DateTime UpdatedAtUtc { get; init; }

    public required string Sku { get; init; }
    public required string Name { get; init; }

    [StoredAsDecimal]
    public required string Price { get; init; }
}