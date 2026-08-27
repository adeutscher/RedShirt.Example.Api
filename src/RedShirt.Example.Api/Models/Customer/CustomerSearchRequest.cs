namespace RedShirt.Example.Api.Models.Customer;

public class CustomerSearchRequest
{
    public int PageSize { get; set; }
    public DateTime? CreatedBeforeUtc { get; set; }
    public DateTime? CreatedAfterUtc { get; set; }
    public DateTime? UpdatedBeforeUtc { get; set; }
    public DateTime? UpdatedAfterUtc { get; set; }
    public Guid? Id { get; set; }
    public string? Email { get; set; }
    public string? EmailContains { get; set; }
    public string? DisplayName { get; set; }
    public string? DisplayNameContains { get; set; }
    public Guid? ContinuationToken { get; set; }
}