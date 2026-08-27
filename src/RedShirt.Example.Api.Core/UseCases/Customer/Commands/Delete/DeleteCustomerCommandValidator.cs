using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Customer.Commands.Delete;

public class DeleteCustomerCommandValidator : AbstractValidator<DeleteCustomerCommand>
{
    public DeleteCustomerCommandValidator()
    {
        RuleFor(command => command.Id)
            .Must(id => id != Guid.Empty)
            .WithMessage("Id is required");
    }
}