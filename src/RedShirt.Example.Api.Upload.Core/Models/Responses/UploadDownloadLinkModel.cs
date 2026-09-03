namespace RedShirt.Example.Api.Upload.Core.Models.Responses;

public sealed class UploadDownloadLinkModel
{
    public required string DownloadUrl { get; init; }
    public required DateTime ExpiresAtUtc { get; init; }
}