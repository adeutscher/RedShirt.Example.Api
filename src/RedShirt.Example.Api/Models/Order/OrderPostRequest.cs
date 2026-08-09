namespace RedShirt.Example.Api.Models.Order;

public class OrderPostRequest
{
    public string? TotalPrice { get; set; }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
    public Guid CustomerId { get; set; }
    public string Status { get; set; }
    public string TotalAmount { get; set; }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
}