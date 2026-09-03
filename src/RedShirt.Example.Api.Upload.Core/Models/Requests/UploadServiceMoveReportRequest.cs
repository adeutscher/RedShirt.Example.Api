namespace RedShirt.Example.Api.Upload.Core.Models.Requests;

public sealed class UploadServiceMoveReportRequest
{
    public required Guid UploadId { get; init; }
    public required string VerifiedStorageObjectKey { get; init; }
}