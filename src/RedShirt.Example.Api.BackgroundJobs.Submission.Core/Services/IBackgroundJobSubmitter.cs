using RedShirt.Example.Api.BackgroundJobs.Submission.Core.Models.Parameters;

namespace RedShirt.Example.Api.BackgroundJobs.Submission.Core.Services;

/// <summary>
///     The one way that features inside the API send background work. Callers know nothing about where the job goes.
///     <para>
///         This is the substitution point between the in-API job table and a queue. The baseline implementation writes to
///         the BackgroundJob table, where the in-API runner picks the job up. An implementation that publishes to a queue
///         read by an applied JobWorker can replace it without changing any caller.
///     </para>
/// </summary>
public interface IBackgroundJobSubmitter
{
    /// <returns>The submitted job's id (a message id for a queue backing).</returns>
    Task<Guid> SubmitAsync<TParameters>(TParameters parameters, CancellationToken cancellationToken = default)
        where TParameters : IBackgroundJobParameters;
}