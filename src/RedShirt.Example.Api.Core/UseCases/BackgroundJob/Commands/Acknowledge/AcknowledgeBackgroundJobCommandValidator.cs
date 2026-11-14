using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Acknowledge;

public class AcknowledgeBackgroundJobCommandValidator : AbstractValidator<AcknowledgeBackgroundJobCommand>
{
    public AcknowledgeBackgroundJobCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.LeaseOwner).NotEmpty().MaximumLength(BackgroundJobValidationLimits.LeaseOwnerMaxLength);
        RuleFor(x => x.Attempt).GreaterThanOrEqualTo(1);
        RuleFor(x => x.Result).IsInEnum();
    }
}
