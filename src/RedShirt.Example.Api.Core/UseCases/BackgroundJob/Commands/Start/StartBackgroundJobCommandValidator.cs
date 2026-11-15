using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Start;

public class StartBackgroundJobCommandValidator : AbstractValidator<StartBackgroundJobCommand>
{
    public StartBackgroundJobCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Attempt).GreaterThanOrEqualTo(1);
    }
}
