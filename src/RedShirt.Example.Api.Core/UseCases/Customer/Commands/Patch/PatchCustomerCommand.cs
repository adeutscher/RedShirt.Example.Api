namespace RedShirt.Example.Api.Core.UseCases.Customer.Commands.Patch;

public record PatchCustomerCommand(Guid Id, string? Email, string? DisplayName);
