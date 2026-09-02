using FluentValidation;
using RedShirt.Example.Api.Core.Cqrs;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Queries.SearchRecords;

public class SearchUploadRecordsQueryValidator : AbstractValidator<SearchUploadRecordsQuery>
{
    public SearchUploadRecordsQueryValidator()
    {
        RuleFor(x => x.Parameters).NotNull();
    }
}
