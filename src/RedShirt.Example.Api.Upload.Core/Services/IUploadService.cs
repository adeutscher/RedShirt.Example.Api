using RedShirt.Example.Api.Upload.Core.Models;

namespace RedShirt.Example.Api.Upload.Core.Services;

public sealed class UploadServiceCreateRequest
{
    public required string FileName { get; init; }
    public required string UploadedByUserId { get; init; }
    public required string UploadedByUsername { get; init; }
    public required string UploaderIpAddress { get; init; }
    public required Stream Content { get; init; }
}

public sealed class UploadServiceSearchRequest
{
    public int PageSize { get; init; }
    public DateTime? CreatedBeforeUtc { get; init; }
    public DateTime? CreatedAfterUtc { get; init; }
    public DateTime? UpdatedBeforeUtc { get; init; }
    public DateTime? UpdatedAfterUtc { get; init; }
    public Guid? Id { get; init; }
    public UploadState? State { get; init; }
    public string? UploadedByUserId { get; init; }
    public string? FileName { get; init; }
    public bool? IsValidated { get; init; }
    public bool? IsRejected { get; init; }
}

public sealed class UploadServiceVerdictRequest
{
    public required Guid UploadId { get; init; }
    public required bool Approved { get; init; }
}

public sealed class UploadServiceMoveReportRequest
{
    public required Guid UploadId { get; init; }
    public required string VerifiedStorageObjectKey { get; init; }
}

public interface IUploadService
{
    Task<UploadSummaryModel> CreateAsync(UploadServiceCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<UploadSummaryModel> GetSummaryAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UploadDetailsModel> GetDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UploadSearchResponse> SearchAsync(UploadServiceSearchRequest parameters, Guid? continuationToken,
        CancellationToken cancellationToken = default);

    Task<UploadSummaryModel> SubmitVerdictAsync(UploadServiceVerdictRequest request,
        CancellationToken cancellationToken = default);

    Task<UploadSummaryModel> SubmitMoveReportAsync(UploadServiceMoveReportRequest request,
        CancellationToken cancellationToken = default);

    Task<UploadSummaryModel> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<UploadDownloadLinkModel> GetDownloadLinkAsync(Guid id, CancellationToken cancellationToken = default);
}
