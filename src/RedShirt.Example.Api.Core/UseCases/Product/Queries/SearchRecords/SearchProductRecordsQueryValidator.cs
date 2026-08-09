using FluentValidation;
using RedShirt.Example.Api.Core.Extensions.Validation;

namespace RedShirt.Example.Api.Core.UseCases.Product.Queries.SearchRecords;

public class SearchProductRecordsQueryValidator : AbstractValidator<SearchProductRecordsQuery>
{
    public SearchProductRecordsQueryValidator()
    {
        RuleFor(query => query.Parameters.Price)
            .MustBeValidStoredDecimalWhenPresent();

        RuleFor(query => query.Parameters.PriceGreaterThan)
            .MustBeValidStoredDecimalWhenPresent();

        RuleFor(query => query.Parameters.PriceLessThan)
            .MustBeValidStoredDecimalWhenPresent();
    }
}
