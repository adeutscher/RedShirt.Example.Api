using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Foo.Queries.GetRecord;

public class GetFooRecordQueryValidator : AbstractValidator<GetFooRecordQuery>
{
    public GetFooRecordQueryValidator()
    {
        RuleFor(query => query.Id)
            .Must(id => id > 0)
            .WithMessage("Id must be greater than zero");
    }
}