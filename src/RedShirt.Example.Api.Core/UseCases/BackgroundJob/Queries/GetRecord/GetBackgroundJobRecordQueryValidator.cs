using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Queries.GetRecord;

public class GetBackgroundJobRecordQueryValidator : AbstractValidator<GetBackgroundJobRecordQuery>
{
    public GetBackgroundJobRecordQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
