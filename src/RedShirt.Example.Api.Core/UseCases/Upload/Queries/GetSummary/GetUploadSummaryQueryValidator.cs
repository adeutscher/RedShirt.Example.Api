using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetSummary;

public class GetUploadSummaryQueryValidator : AbstractValidator<GetUploadSummaryQuery>
{
    public GetUploadSummaryQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}