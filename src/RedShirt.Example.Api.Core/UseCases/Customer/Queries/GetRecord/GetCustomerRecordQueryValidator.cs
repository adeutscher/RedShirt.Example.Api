using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Customer.Queries.GetRecord;

public class GetCustomerRecordQueryValidator : AbstractValidator<GetCustomerRecordQuery>
{
    public GetCustomerRecordQueryValidator()
    {
        RuleFor(query => query.Id)
            .Must(id => id != Guid.Empty)
            .WithMessage("Id is required");
    }
}
