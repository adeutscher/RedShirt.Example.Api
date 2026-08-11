namespace RedShirt.Example.Api.Connectors.Foo.Core.Exceptions;

/// <summary>
///     The Foo dependency rejected the API key (HTTP 401), including after a force-refresh attempt.
/// </summary>
public sealed class FooUnauthorizedException() : Exception("Foo API rejected the API key.");