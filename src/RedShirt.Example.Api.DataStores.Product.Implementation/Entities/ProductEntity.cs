using RedShirt.Example.Api.Common.Analyzers.Database.Abstractions.Attributes;
using RedShirt.Example.Api.DataStores.Constants;

namespace RedShirt.Example.Api.DataStores.Product.Implementation.Entities;

/// <summary>
///     Dapper persistence model for a product.
/// </summary>
[DbTable("Product", DatabaseConstants.PrimaryDatabaseConnectionStringName)]
internal sealed class ProductEntity
{
    [DbKey]
    public required Guid Id { get; init; }

    [CreatedAtProperty]
    public required DateTime CreatedAtUtc { get; init; }

    [UpdatedAtProperty]
    public required DateTime UpdatedAtUtc { get; init; }

    public required string Sku { get; init; }
    public required string Name { get; init; }

    public required decimal Price { get; init; }
}