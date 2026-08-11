using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Bar.Commands.Create;

public class CreateBarCommandValidator : AbstractValidator<CreateBarCommand>
{
    public CreateBarCommandValidator()
    {
        RuleFor(command => command.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required");
    }
}