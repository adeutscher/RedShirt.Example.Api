using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Customer.Commands.Patch;

public class PatchCustomerCommandValidator : AbstractValidator<PatchCustomerCommand>
{
    public PatchCustomerCommandValidator()
    {
        RuleFor(command => command.Id)
            .Must(id => id != Guid.Empty)
            .WithMessage("Id is required");
    }
}
