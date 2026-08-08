using RedShirt.Example.Api.Common.Analyzers.Database.Abstractions.Attributes;
using RedShirt.Example.Api.Implementations.Constants;

namespace RedShirt.Example.Api.Implementations.Orders.Models;

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
    public required decimal TotalAmount { get; init; }
}