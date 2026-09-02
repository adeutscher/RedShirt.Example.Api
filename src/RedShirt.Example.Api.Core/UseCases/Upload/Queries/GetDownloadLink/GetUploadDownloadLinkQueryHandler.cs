using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.Services;
using RedShirt.Example.Api.Upload.Core.Models;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetDownloadLink;

public sealed record GetUploadDownloadLinkQuery(Guid Id);

public interface IGetUploadDownloadLinkQueryHandler : ICqrsHandler<GetUploadDownloadLinkQuery, UploadDownloadLinkModel>;

internal sealed class GetUploadDownloadLinkQueryHandler(
    IUploadService uploadService,
    ICoreRequestValidator coreRequestValidator) : IGetUploadDownloadLinkQueryHandler
{
    public async Task<UploadDownloadLinkModel> Handle(GetUploadDownloadLinkQuery query,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(query, cancellationToken);
        return await uploadService.GetDownloadLinkAsync(query.Id, cancellationToken);
    }
}
