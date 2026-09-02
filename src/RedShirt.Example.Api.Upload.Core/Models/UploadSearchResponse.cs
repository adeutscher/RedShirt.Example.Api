namespace RedShirt.Example.Api.Upload.Core.Models;

public sealed class UploadSearchResponse
{
    public required IReadOnlyList<UploadSummaryModel> Records { get; init; }
    public Guid? ContinuationToken { get; init; }
}
