using System.Text.Json.Serialization;

namespace RedShirt.Example.Api.BackgroundJobs.Submission.Core.Models;

/// <summary>
///     The kinds of background job. Picks the handler that runs a job.
///     Stored as an integer, so values are explicit and must never be renumbered.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BackgroundJobType
{
    /// <summary>
    ///     Check a completed upload and submit a verdict on it.
    /// </summary>
    ValidateUpload = 1,

    /// <summary>
    ///     Move a verified upload into the verified bucket and record it as stored.
    /// </summary>
    MoveUpload = 2

    // When adding a type, also add its parameters model (IBackgroundJobParameters) and a handler.
}
