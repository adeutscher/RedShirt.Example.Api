using FluentValidation;
using RedShirt.Example.Api.Core.Cqrs;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetDownloadLink;

public class GetUploadDownloadLinkQueryValidator : AbstractValidator<GetUploadDownloadLinkQuery>
{
    public GetUploadDownloadLinkQueryValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
