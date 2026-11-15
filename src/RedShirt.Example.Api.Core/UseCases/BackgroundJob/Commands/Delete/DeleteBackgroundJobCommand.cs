namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Delete;

/// <summary>
///     Deletes a finished (succeeded or failed) background job.
/// </summary>
public sealed record DeleteBackgroundJobCommand(Guid Id);