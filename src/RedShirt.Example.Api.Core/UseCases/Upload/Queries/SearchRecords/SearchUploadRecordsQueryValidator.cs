using FluentValidation;
using RedShirt.Example.Api.Core.Extensions.Validation;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Queries.SearchRecords;

public class SearchUploadRecordsQueryValidator : AbstractValidator<SearchUploadRecordsQuery>
{
    public SearchUploadRecordsQueryValidator()
    {
        RuleFor(x => x.FileName).MustBePosixCompliantFileNameWhenPresent();
        RuleFor(x => x.Sha256Checksum).MustBeValidSha256ChecksumWhenPresent();
    }
}