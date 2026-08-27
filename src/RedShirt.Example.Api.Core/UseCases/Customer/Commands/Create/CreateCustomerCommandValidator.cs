using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Customer.Commands.Create;

public class CreateCustomerCommandValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerCommandValidator()
    {
        RuleFor(command => command.Email)
            .Must(email => !string.IsNullOrWhiteSpace(email))
            .WithMessage("Email is required");

        RuleFor(command => command.DisplayName)
            .Must(displayName => !string.IsNullOrWhiteSpace(displayName))
            .WithMessage("DisplayName is required");
    }
}