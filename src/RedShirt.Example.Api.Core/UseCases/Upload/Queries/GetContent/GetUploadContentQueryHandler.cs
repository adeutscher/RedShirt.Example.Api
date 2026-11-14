using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Queries.GetContent;

public sealed record GetUploadContentQuery(Guid Id);

/// <summary>
///     Opens an upload's content for reading inside the API. Not exposed as an endpoint: API clients download through
///     <c>/uploads/{id}/download-link</c> instead. The caller disposes the returned stream.
/// </summary>
public interface IGetUploadContentQueryHandler : ICqrsHandler<GetUploadContentQuery, Stream>;

internal sealed class GetUploadContentQueryHandler(
    IUploadService uploadService,
    ICoreRequestValidator coreRequestValidator) : IGetUploadContentQueryHandler
{
    public async Task<Stream> Handle(GetUploadContentQuery query, CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(query, cancellationToken);
        return await uploadService.OpenContentAsync(query.Id, cancellationToken);
    }
}
