using RedShirt.Example.Api.Upload.Core.Models;

namespace RedShirt.Example.Api.Upload.Core.Models.Requests;

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
