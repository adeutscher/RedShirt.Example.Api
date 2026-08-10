using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Foo.Commands.Create;

public class CreateFooCommandValidator : AbstractValidator<CreateFooCommand>
{
    public CreateFooCommandValidator()
    {
        RuleFor(command => command.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required");
    }
}
