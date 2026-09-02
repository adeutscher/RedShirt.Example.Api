namespace RedShirt.Example.Api.Upload.Core.Models.Requests;

public sealed class UploadServiceVerdictRequest
{
    public required Guid UploadId { get; init; }
    public required bool Approved { get; init; }
}