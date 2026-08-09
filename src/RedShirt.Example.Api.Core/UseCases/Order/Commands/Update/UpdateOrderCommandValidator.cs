using FluentValidation;
using RedShirt.Example.Api.Core.Extensions.Validation;

namespace RedShirt.Example.Api.Core.UseCases.Order.Commands.Update;

public class UpdateOrderCommandValidator : AbstractValidator<UpdateOrderCommand>
{
    public UpdateOrderCommandValidator()
    {
        RuleFor(command => command.Id)
            .Must(id => id != Guid.Empty)
            .WithMessage("Id is required");

        RuleFor(command => command.CustomerId)
            .Must(id => id != Guid.Empty)
            .WithMessage("CustomerId is required");

        RuleFor(command => command.Status)
            .Must(status => !string.IsNullOrWhiteSpace(status))
            .WithMessage("Status is required");

        RuleFor(command => command.TotalAmount)
            .Cascade(CascadeMode.Stop)
            .Must(totalAmount => !string.IsNullOrWhiteSpace(totalAmount))
            .WithMessage("TotalAmount is required")
            .MustBeValidStoredDecimal();

        RuleFor(command => command.TotalPrice)
            .MustBeValidStoredDecimalWhenPresent();
    }
}
