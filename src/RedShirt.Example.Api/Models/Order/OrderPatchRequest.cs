using RedShirt.Example.Api.DataStores.Analyzers.Abstractions.Attributes;

namespace RedShirt.Example.Api.Models.Order;

public class OrderPatchRequest
{
    public Guid? CustomerId { get; set; }
    public string? Status { get; set; }

    [StoredAsDecimal]
    public string? TotalAmount { get; set; }

    [StoredAsDecimal]
    public string? TotalPrice { get; set; }

    public bool? ClearTotalPrice { get; set; }
}
