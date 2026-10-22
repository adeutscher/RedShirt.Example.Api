namespace RedShirt.Example.Api.Models.Order;

public class OrderPatchRequest
{
    public Guid? CustomerId { get; set; }
    public string? Status { get; set; }
    public decimal? TotalAmount { get; set; }
    public decimal? TotalPrice { get; set; }
    public bool? ClearTotalPrice { get; set; }
}