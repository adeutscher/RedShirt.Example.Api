using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Product.Commands.Create;

public class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(command => command.Sku)
            .Must(sku => !string.IsNullOrWhiteSpace(sku))
            .WithMessage("Sku is required");

        RuleFor(command => command.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required");

        RuleFor(command => command.Price)
            .Must(price => !string.IsNullOrWhiteSpace(price))
            .WithMessage("Price is required");
    }
}
