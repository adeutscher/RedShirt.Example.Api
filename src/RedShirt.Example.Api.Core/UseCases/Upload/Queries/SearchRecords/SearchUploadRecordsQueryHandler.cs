using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Core.Services;
using RedShirt.Example.Api.Upload.Core.Models;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Queries.SearchRecords;

public sealed record SearchUploadRecordsQuery(UploadServiceSearchRequest Parameters, Guid? ContinuationToken);

public interface ISearchUploadRecordsQueryHandler : ICqrsHandler<SearchUploadRecordsQuery, UploadSearchResponse>;

internal sealed class SearchUploadRecordsQueryHandler(
    IUploadService uploadService,
    ICoreRequestValidator coreRequestValidator) : ISearchUploadRecordsQueryHandler
{
    public async Task<UploadSearchResponse> Handle(SearchUploadRecordsQuery query,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(query, cancellationToken);
        return await uploadService.SearchAsync(query.Parameters, query.ContinuationToken, cancellationToken);
    }
}
