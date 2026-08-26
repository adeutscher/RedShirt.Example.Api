namespace RedShirt.Example.Api.Core.UseCases.Customer.Commands.Create;

public record CreateCustomerCommand(string Email, string DisplayName, string IdempotencyKey);
