namespace RedShirt.Example.Api.DataStores.Customer.Core.Models;

public class CustomerServiceSearchRequest
{
    public required int PageSize { get; init; }
    public required DateTime? CreatedBeforeUtc { get; init; }
    public required DateTime? CreatedAfterUtc { get; init; }
    public required DateTime? UpdatedBeforeUtc { get; init; }
    public required DateTime? UpdatedAfterUtc { get; init; }
    public required Guid? Id { get; init; }
    public required string? Email { get; init; }
    public required string? EmailContains { get; init; }
    public required string? DisplayName { get; init; }
    public required string? DisplayNameContains { get; init; }
}
