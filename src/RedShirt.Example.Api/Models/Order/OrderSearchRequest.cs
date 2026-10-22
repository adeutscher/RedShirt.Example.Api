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
    public decimal? TotalAmount { get; set; }
    public decimal? TotalAmountGreaterThan { get; set; }
    public decimal? TotalAmountLessThan { get; set; }
    public decimal? TotalPrice { get; set; }
    public decimal? TotalPriceGreaterThan { get; set; }
    public decimal? TotalPriceLessThan { get; set; }
    public bool TotalPriceIsNull { get; set; }
    public Guid? ContinuationToken { get; set; }
}