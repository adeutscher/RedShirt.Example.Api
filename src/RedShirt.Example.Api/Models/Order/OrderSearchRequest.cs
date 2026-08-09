namespace RedShirt.Example.Api.Models.Order;

public class OrderSearchRequest
{
    public int PageSize { get; set; }
    public DateTime? CreatedBeforeUtc { get; set; }
    public DateTime? CreatedAfterUtc { get; set; }
    public DateTime? UpdatedBeforeUtc { get; set; }
    public DateTime? UpdatedAfterUtc { get; set; }
    public Guid? CustomerId { get; set; }
    public string? Status { get; set; }
    public string? StatusContains { get; set; }
    public string? TotalAmount { get; set; }
    public string? TotalAmountGreaterThan { get; set; }
    public string? TotalAmountLessThan { get; set; }
    public string? TotalPrice { get; set; }
    public string? TotalPriceGreaterThan { get; set; }
    public string? TotalPriceLessThan { get; set; }
    public bool TotalPriceIsNull { get; set; }
    public Guid? ContinuationToken { get; set; }
}