using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Customer.Commands.Update;

public class UpdateCustomerCommandValidator : AbstractValidator<UpdateCustomerCommand>
{
    public UpdateCustomerCommandValidator()
    {
        RuleFor(command => command.Id)
            .Must(id => id != Guid.Empty)
            .WithMessage("Id is required");

        RuleFor(command => command.Email)
            .Must(email => !string.IsNullOrWhiteSpace(email))
            .WithMessage("Email is required");

        RuleFor(command => command.DisplayName)
            .Must(displayName => !string.IsNullOrWhiteSpace(displayName))
            .WithMessage("DisplayName is required");
    }
}
