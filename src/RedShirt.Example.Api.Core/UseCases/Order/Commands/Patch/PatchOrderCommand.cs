namespace RedShirt.Example.Api.Core.UseCases.Order.Commands.Patch;

public record PatchOrderCommand(Guid Id, Guid? CustomerId, string? Status, string? TotalAmount);
