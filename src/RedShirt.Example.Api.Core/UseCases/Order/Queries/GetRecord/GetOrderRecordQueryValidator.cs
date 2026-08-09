using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Order.Queries.GetRecord;

public class GetOrderRecordQueryValidator : AbstractValidator<GetOrderRecordQuery>
{
    public GetOrderRecordQueryValidator()
    {
        RuleFor(query => query.Id)
            .Must(id => id != Guid.Empty)
            .WithMessage("Id is required");
    }
}