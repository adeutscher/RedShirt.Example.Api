namespace RedShirt.Example.Api.DataStores.Customer.Core.Models;

public class CustomerDto
{
    public required Guid Id { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required DateTime UpdatedAtUtc { get; init; }
    public required string Email { get; init; }
    public required string DisplayName { get; init; }
}
