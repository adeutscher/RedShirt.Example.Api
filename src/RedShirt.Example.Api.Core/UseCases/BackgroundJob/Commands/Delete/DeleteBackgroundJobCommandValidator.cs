using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Delete;

public class DeleteBackgroundJobCommandValidator : AbstractValidator<DeleteBackgroundJobCommand>
{
    public DeleteBackgroundJobCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}