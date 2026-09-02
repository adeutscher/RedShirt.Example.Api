namespace RedShirt.Example.Api.Upload.Core.Models.Requests;

public sealed class UploadServiceCreateRequest
{
    public required string FileName { get; init; }
    public required string UploadedByUserId { get; init; }
    public required string UploadedByUsername { get; init; }
    public required string UploaderIpAddress { get; init; }
    public required Stream Content { get; init; }
    public required string IdempotencyKey { get; init; }
}