namespace RedShirt.Example.Api.Upload.Core.Models;

public sealed class UploadEventRecordModel
{
    public required Guid Id { get; init; }
    public required DateTime EventDateUtc { get; init; }
    public required string EventType { get; init; }
    public required string Json { get; init; }
}

public sealed class UploadDetailsModel
{
    public required Guid Id { get; init; }
    public required IReadOnlyList<UploadEventRecordModel> Events { get; init; }
}
