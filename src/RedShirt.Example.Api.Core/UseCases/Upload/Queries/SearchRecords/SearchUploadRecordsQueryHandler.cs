using RedShirt.Example.Api.Core.Cqrs;
using RedShirt.Example.Api.Upload.Core.Models.Requests;
using RedShirt.Example.Api.Upload.Core.Models.Responses;
using RedShirt.Example.Api.Upload.Core.Services;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Queries.SearchRecords;

public interface ISearchUploadRecordsQueryHandler : ICqrsHandler<SearchUploadRecordsQuery, UploadSearchResponse>;

internal sealed class SearchUploadRecordsQueryHandler(
    IUploadService uploadService,
    ICoreRequestValidator coreRequestValidator) : ISearchUploadRecordsQueryHandler
{
    public async Task<UploadSearchResponse> Handle(SearchUploadRecordsQuery query,
        CancellationToken cancellationToken = default)
    {
        await coreRequestValidator.ValidateAsync(query, cancellationToken);
        return await uploadService.SearchAsync(new UploadServiceSearchRequest
        {
            PageSize = query.PageSize,
            CreatedBeforeUtc = query.CreatedBeforeUtc,
            CreatedAfterUtc = query.CreatedAfterUtc,
            UpdatedBeforeUtc = query.UpdatedBeforeUtc,
            UpdatedAfterUtc = query.UpdatedAfterUtc,
            Id = query.Id,
            State = query.State,
            UploadedByUserId = query.UploadedByUserId,
            FileName = query.FileName,
            Sha256Checksum = query.Sha256Checksum,
            FileSizeBytes = query.FileSizeBytes,
            FileSizeBytesGreaterThan = query.FileSizeBytesGreaterThan,
            FileSizeBytesLessThan = query.FileSizeBytesLessThan,
            IsValidated = query.IsValidated,
            IsRejected = query.IsRejected
        }, query.ContinuationToken, cancellationToken);
    }
}