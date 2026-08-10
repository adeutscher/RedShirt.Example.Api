using System.Net;

namespace RedShirt.Api.Example.Connectors.Common.Http.Exceptions;

/// <summary>
///     The OAuth token endpoint returned a non-success HTTP status.
/// </summary>
public sealed class OAuthRequestException : Exception
{
    public HttpStatusCode? StatusCode { get; }

    public OAuthRequestException(string message, HttpStatusCode? statusCode = null)
        : base(message)
    {
        StatusCode = statusCode;
    }

    public OAuthRequestException(string message, Exception innerException, HttpStatusCode? statusCode = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }
}
