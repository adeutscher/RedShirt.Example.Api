namespace RedShirt.Example.Api.Connectors.Foo.Implementation.Exceptions;

/// <summary>
///     The Foo dependency rejected the API key (HTTP 401), including after a force-refresh attempt.
///     Surfaced to callers as <see cref="Core.Exceptions.FooConnectorException" /> by the connector retry wrapper.
/// </summary>
internal sealed class FooUnauthorizedException() : Exception("Foo API rejected the API key.");
