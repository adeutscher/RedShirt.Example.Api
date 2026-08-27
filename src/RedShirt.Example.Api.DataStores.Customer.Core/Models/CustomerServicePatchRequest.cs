namespace RedShirt.Example.Api.DataStores.Customer.Core.Models;

public class CustomerServicePatchRequest
{
    public required Guid Id { get; init; }
    public required string? Email { get; init; }
    public required string? DisplayName { get; init; }
}