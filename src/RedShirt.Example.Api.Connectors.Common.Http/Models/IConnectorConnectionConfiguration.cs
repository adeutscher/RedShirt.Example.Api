namespace RedShirt.Example.Api.Connectors.Common.Http.Models;

/// <summary>
///     Connection settings shared by every connector's configuration model.
/// </summary>
public interface IConnectorConnectionConfiguration
{
    /// <summary>
    ///     When true, every connector attempt builds its own <c>HttpClient</c> and handler and disposes them
    ///     afterwards, so no connection is reused. When false, attempts share the connector's pooled named client.
    /// </summary>
    bool ClientPerRequest { get; }

    /// <summary>
    ///     Seconds the pooled handler chain is used before it is replaced, so DNS changes are picked up. Must be at
    ///     least 1. Only applies when <see cref="ClientPerRequest" /> is false.
    /// </summary>
    int HandlerLifetimeSeconds { get; }

    /// <summary>
    ///     Most connections open to the dependency at once. Requests over the cap wait for a free connection, and
    ///     that wait counts against the request timeout. 0 or less means no cap. Only applies when
    ///     <see cref="ClientPerRequest" /> is false.
    /// </summary>
    int MaxConnectionsPerServer { get; }
}