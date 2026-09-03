using RedShirt.Example.Api.Upload.Core.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace RedShirt.Example.Api.Upload.Implementation.Entities;

[Table("UploadAggregate")]
internal sealed class UploadAggregateEntity
{
    public required Guid Id { get; init; }
    public required DateTime DateCreatedUtc { get; set; }
    public required DateTime DateUpdatedUtc { get; set; }
    public required string UploadedByUserId { get; set; }
    public required UploadState State { get; set; }
    public required string FileName { get; set; }
    public long? FileSizeBytes { get; set; }
    public required UploadAggregateFlags Flags { get; set; }
    public string? Sha256Checksum { get; set; }
    public required string IdempotencyKey { get; init; }
}