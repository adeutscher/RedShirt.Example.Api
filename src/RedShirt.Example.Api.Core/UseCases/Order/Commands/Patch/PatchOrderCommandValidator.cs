using FluentValidation;
using RedShirt.Example.Api.Core.Extensions.Validation;

namespace RedShirt.Example.Api.Core.UseCases.Order.Commands.Patch;

public class PatchOrderCommandValidator : AbstractValidator<PatchOrderCommand>
{
    public PatchOrderCommandValidator()
    {
        RuleFor(command => command.Id)
            .Must(id => id != Guid.Empty)
            .WithMessage("Id is required");

        RuleFor(command => command.TotalAmount)
            .MustBeValidStoredDecimalWhenPresent();
    }
}
