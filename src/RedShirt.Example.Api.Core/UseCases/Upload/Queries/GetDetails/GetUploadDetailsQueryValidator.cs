using FluentValidation;
using RedShirt.Example.Api.Core.Cqrs;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetDetails;

public class GetUploadDetailsQueryValidator : AbstractValidator<GetUploadDetailsQuery>
{
    public GetUploadDetailsQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
