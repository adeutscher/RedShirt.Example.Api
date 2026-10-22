using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Order.Commands.Create;

public class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(command => command.Status)
            .Must(status => !string.IsNullOrWhiteSpace(status))
            .WithMessage("'Status' must not be empty.");
    }
}
