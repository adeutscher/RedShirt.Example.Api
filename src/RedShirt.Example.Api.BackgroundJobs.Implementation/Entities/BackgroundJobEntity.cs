using RedShirt.Example.Api.BackgroundJobs.Core.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace RedShirt.Example.Api.BackgroundJobs.Implementation.Entities;

[Table("BackgroundJob")]
internal sealed class BackgroundJobEntity
{
    public required Guid Id { get; init; }
    public required DateTime DateCreatedUtc { get; init; }
    public required DateTime DateUpdatedUtc { get; set; }
    public required BackgroundJobType Type { get; init; }
    public required BackgroundJobState State { get; set; }
    public required string ParametersJson { get; init; }
    public required int Attempts { get; set; }
    public required DateTime DueAtUtc { get; set; }
    public string? LeaseOwner { get; set; }
    public DateTime? LeaseExpiresAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? Detail { get; set; }
}
