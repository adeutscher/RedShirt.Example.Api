using RedShirt.Example.Api.Upload.Core.Models.Requests;
using RedShirt.Example.Api.Upload.Core.Models.Responses;

namespace RedShirt.Example.Api.Upload.Core.Services;

public interface IUploadService
{
    Task<UploadSummaryModel> CreateAsync(UploadServiceCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<UploadSummaryModel> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UploadDetailsInternalModel> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UploadDownloadLinkModel> GetDownloadLinkAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UploadSummaryModel> GetSummaryAsync(Guid id, CancellationToken cancellationToken = default);

    Task PurgeAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UploadSearchResponse> SearchAsync(UploadServiceSearchRequest parameters, Guid? continuationToken,
        CancellationToken cancellationToken = default);

    Task<UploadSummaryModel> SubmitMoveReportAsync(UploadServiceMoveReportRequest request,
        CancellationToken cancellationToken = default);

    Task<UploadSummaryModel> SubmitVerdictAsync(UploadServiceVerdictRequest request,
        CancellationToken cancellationToken = default);
}