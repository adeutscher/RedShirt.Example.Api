using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetContent;

public class GetUploadContentQueryValidator : AbstractValidator<GetUploadContentQuery>
{
    public GetUploadContentQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}