using FluentValidation;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetDownloadLink;

public class GetUploadDownloadLinkQueryValidator : AbstractValidator<GetUploadDownloadLinkQuery>
{
    public GetUploadDownloadLinkQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}