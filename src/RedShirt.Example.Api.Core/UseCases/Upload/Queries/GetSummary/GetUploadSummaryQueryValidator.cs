using FluentValidation;
using RedShirt.Example.Api.Core.Cqrs;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetSummary;

public class GetUploadSummaryQueryValidator : AbstractValidator<GetUploadSummaryQuery>
{
    public GetUploadSummaryQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
