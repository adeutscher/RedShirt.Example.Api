using RedShirt.Example.Api.BackgroundJobs.Core.Models;

namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Acknowledge;

public sealed record AcknowledgeBackgroundJobCommand(Guid Id, string LeaseOwner, JobResult Result, string? Detail);
