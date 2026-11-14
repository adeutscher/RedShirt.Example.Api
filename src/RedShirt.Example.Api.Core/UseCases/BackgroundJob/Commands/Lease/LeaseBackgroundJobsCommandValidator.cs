using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Lease;

public class LeaseBackgroundJobsCommandValidator : AbstractValidator<LeaseBackgroundJobsCommand>
{
    public LeaseBackgroundJobsCommandValidator()
    {
        RuleFor(x => x.Count).InclusiveBetween(1, BackgroundJobValidationLimits.MaxLeaseCount);
        RuleFor(x => x.LeaseOwner).NotEmpty().MaximumLength(BackgroundJobValidationLimits.LeaseOwnerMaxLength);
    }
}
