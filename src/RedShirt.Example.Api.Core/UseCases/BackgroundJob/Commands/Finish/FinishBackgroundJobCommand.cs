namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Finish;

/// <summary>
///     Reports that a running attempt of a background job succeeded.
/// </summary>
public sealed record FinishBackgroundJobCommand(Guid Id, int Attempt, string? Detail);
