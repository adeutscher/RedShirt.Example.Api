using RedShirt.Example.Api.Upload.Core.Models.Requests;
using RedShirt.Example.Api.Upload.Core.Models.Responses;

namespace RedShirt.Example.Api.Upload.Core.Services;

public interface IUploadService
{
    /// <summary>
    ///     Copies a <see cref="Models.UploadState.Verified" /> upload's file from the unverified bucket to the verified
    ///     bucket. Does not change the upload's state: report the move with <see cref="SubmitMoveReportAsync" />.
    /// </summary>
    /// <returns>The object key in the verified bucket.</returns>
    Task<string> CopyToVerifiedStorageAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UploadSummaryModel> CreateAsync(UploadServiceCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<UploadSummaryModel> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UploadDetailsInternalModel> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UploadDownloadLinkModel> GetDownloadLinkAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UploadSummaryModel> GetSummaryAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Opens the uploaded file's content for reading, from whichever bucket currently holds it. The caller disposes
    ///     the returned stream.
    /// </summary>
    Task<Stream> OpenContentAsync(Guid id, CancellationToken cancellationToken = default);

    Task PurgeAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Removes the unverified copy of a <see cref="Models.UploadState.Stored" /> upload's file, once the verified copy
    ///     is in place. Safe to repeat.
    /// </summary>
    Task RemoveUnverifiedCopyAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UploadSearchResponse> SearchAsync(UploadServiceSearchRequest parameters, Guid? continuationToken,
        CancellationToken cancellationToken = default);

    Task<UploadSummaryModel> SubmitMoveReportAsync(UploadServiceMoveReportRequest request,
        CancellationToken cancellationToken = default);

    Task<UploadSummaryModel> SubmitVerdictAsync(UploadServiceVerdictRequest request,
        CancellationToken cancellationToken = default);
}