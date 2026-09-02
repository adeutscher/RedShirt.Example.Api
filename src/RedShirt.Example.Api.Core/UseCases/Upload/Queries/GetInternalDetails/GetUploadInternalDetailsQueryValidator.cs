using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetInternalDetails;

public class GetUploadInternalDetailsQueryValidator : AbstractValidator<GetUploadInternalDetailsQuery>
{
    public GetUploadInternalDetailsQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
