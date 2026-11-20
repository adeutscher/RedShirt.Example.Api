namespace RedShirt.Example.Api.Configuration;

/// <summary>
///     Server-Sent Events settings (typically bound from <c>SERVER_SENT_EVENTS__*</c> environment variables).
/// </summary>
public sealed class ServerSentEventsOptions
{
    public const string ConfigurationSectionName = "ServerSentEvents";

    /// <summary>
    ///     Maximum number of event streams one user may hold open at the same time on one API instance. Zero or a
    ///     negative value means unlimited, which is logged as a warning at startup.
    ///     <para>
    ///         The count is kept in memory per instance. Behind a load balancer that spreads one user's streams across
    ///         instances, the effective limit is this value times the number of instances.
    ///     </para>
    /// </summary>
    public int MaxConnectionsPerUser { get; init; }

    /// <summary>
    ///     Whether <see cref="MaxConnectionsPerUser" /> imposes a limit.
    /// </summary>
    public bool IsLimited => MaxConnectionsPerUser > 0;
}