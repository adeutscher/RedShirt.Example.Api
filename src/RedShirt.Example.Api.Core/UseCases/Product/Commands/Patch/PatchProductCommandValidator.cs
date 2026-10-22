using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Product.Commands.Patch;

public class PatchProductCommandValidator : AbstractValidator<PatchProductCommand>
{
    public PatchProductCommandValidator()
    {
    }
}
