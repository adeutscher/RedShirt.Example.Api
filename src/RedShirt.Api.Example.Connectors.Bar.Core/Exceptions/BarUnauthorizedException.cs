namespace RedShirt.Api.Example.Connectors.Bar.Core.Exceptions;

/// <summary>
///     The Bar dependency rejected the API key (HTTP 401), including after a force-refresh attempt.
/// </summary>
public sealed class BarUnauthorizedException() : Exception("Bar API rejected the API key.");