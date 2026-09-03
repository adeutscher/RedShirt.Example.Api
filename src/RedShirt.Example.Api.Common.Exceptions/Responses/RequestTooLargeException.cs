namespace RedShirt.Example.Api.Common.Exceptions.Responses;

/// <summary>
///     Used within services to abort execution when a request exceeds a configured size limit; mapped to an HTTP 413
///     ProblemDetails response by the API exception handler.
/// </summary>
/// <param name="message"></param>
public class RequestTooLargeException(string message) : Exception(message);