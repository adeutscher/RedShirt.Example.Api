using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Order.Commands.Update;

public class UpdateOrderCommandValidator : AbstractValidator<UpdateOrderCommand>
{
    public UpdateOrderCommandValidator()
    {
        RuleFor(command => command.Status)
            .Must(status => !string.IsNullOrWhiteSpace(status))
            .WithMessage("'Status' must not be empty.");
    }
}
