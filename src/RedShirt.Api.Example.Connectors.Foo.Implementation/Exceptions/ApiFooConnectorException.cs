namespace RedShirt.Api.Example.Connectors.Foo.Implementation.Exceptions;

/// <summary>
///     Transport-level failure talking to the Foo dependency (HTTP status, network, timeout, etc.).
/// </summary>
internal class ApiFooConnectorException : Exception
{
    public int? StatusCode { get; }

    public ApiFooConnectorException(int? statusCode)
    {
        StatusCode = statusCode;
    }

    public ApiFooConnectorException(Exception innerException) : base(innerException.Message, innerException)
    {
    }

    public ApiFooConnectorException(int? statusCode, Exception innerException) : base(innerException.Message,
        innerException)
    {
        StatusCode = statusCode;
    }
}
