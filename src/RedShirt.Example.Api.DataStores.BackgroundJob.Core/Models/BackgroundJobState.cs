using System.Text.Json.Serialization;

namespace RedShirt.Example.Api.DataStores.BackgroundJob.Core.Models;

/// <summary>
///     Stored as an integer, so values are explicit and must never be renumbered.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BackgroundJobState
{
    /// <summary>
    ///     Waiting to be leased, possibly not before <see cref="BackgroundJobModel.DueAtUtc" />.
    /// </summary>
    Pending = 0,

    /// <summary>
    ///     Leased by <see cref="BackgroundJobModel.LeaseOwner" /> until <see cref="BackgroundJobModel.LeaseExpiresAtUtc" />.
    /// </summary>
    Running = 1,

    Succeeded = 2,

    /// <summary>
    ///     Will not be tried again unless retried manually.
    /// </summary>
    Failed = 3
}