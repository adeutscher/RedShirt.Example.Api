using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.ExampleItem.Commands.Delete;

public class DeleteExampleItemCommandValidator : AbstractValidator<DeleteExampleItemCommand>
{
    public DeleteExampleItemCommandValidator()
    {
        RuleFor(command => command.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required");
    }
}