namespace RedShirt.Example.Api.Core.UseCases.Order.Commands.Create;

public record CreateOrderCommand(
    Guid CustomerId,
    string Status,
    string TotalAmount,
    string? TotalPrice,
    string IdempotencyKey);