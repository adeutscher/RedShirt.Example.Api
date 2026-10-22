namespace RedShirt.Example.Api.Core.UseCases.Order.Commands.Update;

public record UpdateOrderCommand(
    Guid Id,
    Guid CustomerId,
    string Status,
    decimal TotalAmount,
    decimal? TotalPrice);