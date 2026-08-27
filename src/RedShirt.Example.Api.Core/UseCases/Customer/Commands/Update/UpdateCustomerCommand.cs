namespace RedShirt.Example.Api.Core.UseCases.Customer.Commands.Update;

public record UpdateCustomerCommand(Guid Id, string Email, string DisplayName);