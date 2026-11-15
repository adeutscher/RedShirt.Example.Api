using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Queries.SearchRecords;

public class SearchBackgroundJobRecordsQueryValidator : AbstractValidator<SearchBackgroundJobRecordsQuery>
{
    public SearchBackgroundJobRecordsQueryValidator()
    {
        RuleFor(x => x.PageSize).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Type).IsInEnum().When(x => x.Type.HasValue);
        RuleFor(x => x.State).IsInEnum().When(x => x.State.HasValue);
    }
}