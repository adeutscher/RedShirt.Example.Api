using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.ExampleItem.Commands.Create;

public class CreateExampleItemCommandValidator : AbstractValidator<CreateExampleItemCommand>
{
    public CreateExampleItemCommandValidator()
    {
        RuleFor(command => command.Model.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required");
    }
}