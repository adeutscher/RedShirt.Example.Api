namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Lease;

public sealed record LeaseBackgroundJobsCommand(int Count, string LeaseOwner);