using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Order.Commands.Patch;

public class PatchOrderCommandValidator : AbstractValidator<PatchOrderCommand>
{
    public PatchOrderCommandValidator()
    {
    }
}
