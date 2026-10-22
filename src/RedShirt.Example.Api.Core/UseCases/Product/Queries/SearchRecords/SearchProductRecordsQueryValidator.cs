using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Product.Queries.SearchRecords;

public class SearchProductRecordsQueryValidator : AbstractValidator<SearchProductRecordsQuery>
{
    public SearchProductRecordsQueryValidator()
    {
    }
}
