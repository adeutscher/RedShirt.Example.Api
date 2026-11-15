using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Finish;

public class FinishBackgroundJobCommandValidator : AbstractValidator<FinishBackgroundJobCommand>
{
    public FinishBackgroundJobCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Attempt).GreaterThanOrEqualTo(1);
    }
}
