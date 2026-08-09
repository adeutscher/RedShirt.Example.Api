using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Product.Commands.Patch;

public class PatchProductCommandValidator : AbstractValidator<PatchProductCommand>
{
    public PatchProductCommandValidator()
    {
        RuleFor(command => command.Id)
            .Must(id => id != Guid.Empty)
            .WithMessage("Id is required");
    }
}