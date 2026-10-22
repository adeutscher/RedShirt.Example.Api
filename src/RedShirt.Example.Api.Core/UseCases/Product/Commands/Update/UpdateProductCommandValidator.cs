using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Product.Commands.Update;

public class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(command => command.Sku)
            .Must(sku => !string.IsNullOrWhiteSpace(sku))
            .WithMessage("Sku is required");

        RuleFor(command => command.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required");
    }
}