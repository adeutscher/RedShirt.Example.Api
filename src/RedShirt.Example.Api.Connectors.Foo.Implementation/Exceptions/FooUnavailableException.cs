namespace RedShirt.Example.Api.Connectors.Foo.Implementation.Exceptions;

/// <summary>
///     Foo is assumed to be unavailable for the time being (for example after auth or API-key
///     recovery failed within the refresh cooldown window).
/// </summary>
internal sealed class FooUnavailableException() : Exception("Foo is assumed to be unavailable for the time being.");