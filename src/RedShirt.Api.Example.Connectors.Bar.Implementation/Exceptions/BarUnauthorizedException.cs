namespace RedShirt.Api.Example.Connectors.Bar.Implementation.Exceptions;

/// <summary>
///     The Bar dependency rejected the bearer token (HTTP 401), including after a force-refresh attempt.
///     Surfaced to callers as <see cref="Core.Exceptions.BarConnectorException" /> by the connector retry wrapper.
/// </summary>
internal sealed class BarUnauthorizedException() : Exception("Bar API rejected the bearer token.");