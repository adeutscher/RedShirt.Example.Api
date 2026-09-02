namespace RedShirt.Example.Api.Upload.Core.Models.Responses;

/// <summary>
///     Internal-only upload metadata for workers and privileged operators.
/// </summary>
public sealed class UploadInternalDetailsModel
{
    public required string UploaderIpAddress { get; init; }

    public string? StorageObjectKey { get; init; }
}
