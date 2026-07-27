using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.ExampleItem.Queries.GetRecord;

public class GetExampleItemRecordQueryValidator : AbstractValidator<GetExampleItemRecordQuery>
{
    public GetExampleItemRecordQueryValidator()
    {
        RuleFor(query => query.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage("Name is required");
    }
}