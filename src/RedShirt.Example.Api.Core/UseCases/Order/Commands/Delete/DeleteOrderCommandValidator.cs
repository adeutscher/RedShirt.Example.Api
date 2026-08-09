using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Order.Commands.Delete;

public class DeleteOrderCommandValidator : AbstractValidator<DeleteOrderCommand>
{
    public DeleteOrderCommandValidator()
    {
        RuleFor(command => command.Id)
            .Must(id => id != Guid.Empty)
            .WithMessage("Id is required");
    }
}
