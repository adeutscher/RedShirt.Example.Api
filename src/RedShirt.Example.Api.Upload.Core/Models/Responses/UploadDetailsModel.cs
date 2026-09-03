namespace RedShirt.Example.Api.Upload.Core.Models.Responses;

/// <summary>
///     Upload details with one nullable field group per lifecycle event (each event type occurs at most once).
/// </summary>
public sealed class UploadDetailsModel
{
    public required Guid Id { get; init; }

    public required DateTime DateCreatedUtc { get; init; }
    public required string UploadedByUserId { get; init; }
    public required string UploadedByUsername { get; init; }
    public required string FileName { get; init; }

    public DateTime? DateCompletedUtc { get; init; }
    public string? Sha256Checksum { get; init; }

    public DateTime? DateValidatedUtc { get; init; }

    public DateTime? DateRejectedUtc { get; init; }

    public DateTime? DateStoredUtc { get; init; }

    public DateTime? DateDeletedUtc { get; init; }
}