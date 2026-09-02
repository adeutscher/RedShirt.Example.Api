using System.ComponentModel.DataAnnotations.Schema;

namespace RedShirt.Example.Api.Upload.Implementation.Entities;

[Table("UploadAggregate")]
internal sealed class UploadAggregateEntity
{
    public required Guid Id { get; init; }
    public required DateTime DateCreatedUtc { get; set; }
    public required DateTime DateUpdatedUtc { get; set; }
    public required string UploadedByUserId { get; set; }
    public required string State { get; set; }
    public required string FileName { get; set; }
    public required bool IsValidated { get; set; }
    public required bool IsRejected { get; set; }
}
