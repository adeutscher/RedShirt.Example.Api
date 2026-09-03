using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetDetails;

public class GetUploadDetailsQueryValidator : AbstractValidator<GetUploadDetailsQuery>
{
    public GetUploadDetailsQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}