using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Fail;

public class FailBackgroundJobCommandValidator : AbstractValidator<FailBackgroundJobCommand>
{
    public FailBackgroundJobCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Attempt).GreaterThanOrEqualTo(1);
    }
}
