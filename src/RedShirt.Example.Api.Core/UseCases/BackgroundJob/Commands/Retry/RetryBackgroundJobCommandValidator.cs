using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Retry;

public class RetryBackgroundJobCommandValidator : AbstractValidator<RetryBackgroundJobCommand>
{
    public RetryBackgroundJobCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}