namespace RedShirt.Example.Api.DataStores.Customer.Core.Models;

public class CustomerServicePostRequest
{
    public required string Email { get; init; }
    public required string DisplayName { get; init; }
}