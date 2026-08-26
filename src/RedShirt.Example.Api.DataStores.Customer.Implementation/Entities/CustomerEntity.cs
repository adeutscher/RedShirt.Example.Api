namespace RedShirt.Example.Api.DataStores.Customer.Implementation.Entities;

/// <summary>
///     Entity Framework persistence model for a customer.
///     Complements the existing Product and Order stores: an order is placed by a customer.
/// </summary>
internal sealed class CustomerEntity
{
    public required Guid Id { get; set; }
    public required DateTime CreatedAtUtc { get; set; }
    public required DateTime UpdatedAtUtc { get; set; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
}
