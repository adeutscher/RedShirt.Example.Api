namespace RedShirt.Example.Api.Upload.Core.Models.Responses;

/// <summary>
///     Summary projection of an upload aggregate, returned by GET <c>/uploads/{id}</c> and search.
/// </summary>
public sealed class UploadSummaryModel
{
    public required Guid Id { get; init; }
    public required DateTime DateCreatedUtc { get; init; }
    public required DateTime DateUpdatedUtc { get; init; }
    public required string UploadedByUserId { get; init; }
    public required UploadState State { get; init; }
    public required string FileName { get; init; }
    public required bool IsValidated { get; init; }
    public required bool IsRejected { get; init; }
    public string? Sha256Checksum { get; init; }
}