namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Fail;

/// <summary>
///     Reports that a running attempt of a background job failed. <see cref="CanRetry" /> is false when no retry can fix
///     the failure (such as unusable parameters), so the job fails without using its remaining attempts.
/// </summary>
public sealed record FailBackgroundJobCommand(Guid Id, int Attempt, bool CanRetry, string? Detail);