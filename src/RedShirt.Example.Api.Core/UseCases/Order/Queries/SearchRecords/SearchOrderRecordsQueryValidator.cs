using FluentValidation;
using RedShirt.Example.Api.Core.Extensions.Validation;

namespace RedShirt.Example.Api.Core.UseCases.Order.Queries.SearchRecords;

public class SearchOrderRecordsQueryValidator : AbstractValidator<SearchOrderRecordsQuery>
{
    public SearchOrderRecordsQueryValidator()
    {
        RuleFor(query => query.TotalAmount)
            .MustBeValidStoredDecimalWhenPresent();

        RuleFor(query => query.TotalAmountGreaterThan)
            .MustBeValidStoredDecimalWhenPresent();

        RuleFor(query => query.TotalAmountLessThan)
            .MustBeValidStoredDecimalWhenPresent();

        RuleFor(query => query.TotalPrice)
            .MustBeValidStoredDecimalWhenPresent();

        RuleFor(query => query.TotalPriceGreaterThan)
            .MustBeValidStoredDecimalWhenPresent();

        RuleFor(query => query.TotalPriceLessThan)
            .MustBeValidStoredDecimalWhenPresent();
    }
}