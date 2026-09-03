using RedShirt.Example.Api.Upload.Core.Models;

namespace RedShirt.Example.Api.Core.UseCases.Upload.Queries.SearchRecords;

public sealed record SearchUploadRecordsQuery(
    int PageSize,
    DateTime? CreatedBeforeUtc,
    DateTime? CreatedAfterUtc,
    DateTime? UpdatedBeforeUtc,
    DateTime? UpdatedAfterUtc,
    Guid? Id,
    UploadState? State,
    string? UploadedByUserId,
    string? FileName,
    string? Sha256Checksum,
    long? FileSizeBytes,
    long? FileSizeBytesGreaterThan,
    long? FileSizeBytesLessThan,
    bool? IsValidated,
    bool? IsRejected,
    Guid? ContinuationToken);