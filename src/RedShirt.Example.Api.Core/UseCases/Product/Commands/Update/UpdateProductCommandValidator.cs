using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Product.Commands.Update;

public class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(command => command.Id)
            .Must(id => id != Guid.Empty)
            .WithMessage("Id is required");

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