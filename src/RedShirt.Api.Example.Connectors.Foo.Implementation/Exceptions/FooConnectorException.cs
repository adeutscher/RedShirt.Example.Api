namespace RedShirt.Api.Example.Connectors.Foo.Implementation.Exceptions;

/// <summary>
///     Transport-level failure talking to the Foo dependency (HTTP status, network, timeout, etc.).
/// </summary>
public class FooConnectorException : Exception
{
    public int? StatusCode { get; }

    public FooConnectorException(int? statusCode)
    {
        StatusCode = statusCode;
    }

    public FooConnectorException(Exception innerException) : base(innerException.Message, innerException)
    {
    }

    public FooConnectorException(int? statusCode, Exception innerException) : base(innerException.Message,
        innerException)
    {
        StatusCode = statusCode;
    }
}
