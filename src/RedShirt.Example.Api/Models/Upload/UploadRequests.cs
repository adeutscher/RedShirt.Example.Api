using RedShirt.Example.Api.Upload.Core.Models;

namespace RedShirt.Example.Api.Models.Upload;

public sealed class UploadSearchRequest
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
    public string? Sha256Checksum { get; init; }
    public bool? IsValidated { get; init; }
    public bool? IsRejected { get; init; }
    public Guid? ContinuationToken { get; init; }
}

public sealed class UploadVerdictRequest
{
    public required bool Approved { get; init; }
}

public sealed class UploadMoveReportRequest
{
    public required string VerifiedStorageObjectKey { get; init; }
}