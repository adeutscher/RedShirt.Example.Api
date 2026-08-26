namespace RedShirt.Example.Api.DataStores.Customer.Core.Models;

public class CustomerSearchResponse
{
    public required Guid? ContinuationToken { get; init; }
    public required List<CustomerDto> Records { get; init; }
}
