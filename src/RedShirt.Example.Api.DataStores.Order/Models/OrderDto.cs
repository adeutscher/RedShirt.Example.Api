using RedShirt.Example.Api.DataStores.Analyzers.Abstractions.Attributes;
using RedShirt.Example.Api.DataStores.Constants;

namespace RedShirt.Example.Api.DataStores.Order.Models;

[DbTable("Order", DatabaseConstants.PrimaryDatabaseConnectionStringName)]
public class OrderDto
{
    [DbKey]
    public required Guid Id { get; init; }

    [CreatedAtProperty]
    public required DateTime CreatedAtUtc { get; init; }

    [UpdatedAtProperty]
    public required DateTime UpdatedAtUtc { get; init; }

    public required Guid CustomerId { get; init; }
    public required string Status { get; init; }

    [StoredAsDecimal]
    public required string TotalAmount { get; init; }

    [StoredAsDecimal]
    public required string? TotalPrice { get; init; }
}