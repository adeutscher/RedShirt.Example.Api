using System.Text.Json.Serialization;

namespace RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;

/// <summary>
///     The outcome of one attempt at a job, as recorded in the BackgroundJob table. Mirrors the JobWorker template's
///     <c>JobResult</c>, which the projects that process jobs map to this, so the table does not depend on them.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BackgroundJobAttemptResult
{
    /// <summary>
    ///     The job completed. It is marked succeeded.
    /// </summary>
    Success,

    /// <summary>
    ///     A recoverable failure. The job is tried again after a back-off, or marked failed once its attempts run out.
    /// </summary>
    Failure,

    /// <summary>
    ///     The job can never succeed with its parameters. It is marked failed without a retry.
    /// </summary>
    InvalidData
}
