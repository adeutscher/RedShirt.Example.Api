using RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;

namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Acknowledge;

public sealed record AcknowledgeBackgroundJobCommand(
    Guid Id,
    string LeaseOwner,
    int Attempt,
    BackgroundJobAttemptResult Result,
    string? Detail);
