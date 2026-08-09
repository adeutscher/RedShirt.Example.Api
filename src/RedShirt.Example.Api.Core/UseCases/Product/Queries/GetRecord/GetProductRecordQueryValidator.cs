using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Product.Queries.GetRecord;

public class GetProductRecordQueryValidator : AbstractValidator<GetProductRecordQuery>
{
    public GetProductRecordQueryValidator()
    {
        RuleFor(query => query.Id)
            .Must(id => id != Guid.Empty)
            .WithMessage("Id is required");
    }
}
