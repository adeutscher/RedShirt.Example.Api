using FluentValidation;
using RedShirt.Example.Api.Core.Extensions.Validation;

namespace RedShirt.Example.Api.Core.UseCases.Order.Queries.SearchRecords;

public class SearchOrderRecordsQueryValidator : AbstractValidator<SearchOrderRecordsQuery>
{
    public SearchOrderRecordsQueryValidator()
    {
        RuleFor(query => query.Parameters.TotalAmount)
            .MustBeValidStoredDecimalWhenPresent();

        RuleFor(query => query.Parameters.TotalAmountGreaterThan)
            .MustBeValidStoredDecimalWhenPresent();

        RuleFor(query => query.Parameters.TotalAmountLessThan)
            .MustBeValidStoredDecimalWhenPresent();

        RuleFor(query => query.Parameters.TotalPrice)
            .MustBeValidStoredDecimalWhenPresent();

        RuleFor(query => query.Parameters.TotalPriceGreaterThan)
            .MustBeValidStoredDecimalWhenPresent();

        RuleFor(query => query.Parameters.TotalPriceLessThan)
            .MustBeValidStoredDecimalWhenPresent();
    }
}
