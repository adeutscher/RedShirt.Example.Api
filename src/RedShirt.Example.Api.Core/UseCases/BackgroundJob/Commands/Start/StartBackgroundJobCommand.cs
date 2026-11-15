namespace RedShirt.Example.Api.Core.UseCases.BackgroundJob.Commands.Start;

/// <summary>
///     Reports that a leased attempt of a background job has started running.
///     <para>
///         <see cref="Attempt" /> is the attempt number that the lease handed out. An applied JobWorker gets its jobs
///         from a queue rather than a lease, so moving there means the start report takes over counting attempts (for
///         example, from the queue's delivery count).
///     </para>
/// </summary>
public sealed record StartBackgroundJobCommand(Guid Id, int Attempt);