using System.ComponentModel.DataAnnotations.Schema;

namespace RedShirt.Example.Api.Upload.Implementation.Entities;

[Table("UploadEvent")]
internal sealed class UploadEventEntity
{
    public required Guid Id { get; set; }
    public required Guid UploadId { get; set; }
    public required DateTime EventDateUtc { get; set; }
    public required string EventType { get; set; }
    public required string Json { get; set; }
}
