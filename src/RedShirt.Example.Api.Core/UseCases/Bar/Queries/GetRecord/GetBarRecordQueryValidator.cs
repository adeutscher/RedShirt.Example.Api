using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Bar.Queries.GetRecord;

public class GetBarRecordQueryValidator : AbstractValidator<GetBarRecordQuery>
{
    public GetBarRecordQueryValidator()
    {
        RuleFor(query => query.Id)
            .Must(id => id > 0)
            .WithMessage("Id must be greater than zero");
    }
}
