namespace RedShirt.Example.Api.Models.Order;

public class OrderPatchRequest
{
    public Guid? CustomerId { get; set; }
    public string? Status { get; set; }
    public string? TotalAmount { get; set; }
}