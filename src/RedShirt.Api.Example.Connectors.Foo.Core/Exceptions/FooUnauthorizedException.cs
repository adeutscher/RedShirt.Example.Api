namespace RedShirt.Api.Example.Connectors.Foo.Core.Exceptions;

/// <summary>
///     The Foo dependency rejected the API key (HTTP 401), including after a force-refresh attempt.
/// </summary>
public sealed class FooUnauthorizedException : Exception
{
    public FooUnauthorizedException()
        : base("Foo API rejected the API key.")
    {
    }

    public FooUnauthorizedException(string message) : base(message)
    {
    }

    public FooUnauthorizedException(Exception innerException)
        : base(innerException.Message, innerException)
    {
    }
}
