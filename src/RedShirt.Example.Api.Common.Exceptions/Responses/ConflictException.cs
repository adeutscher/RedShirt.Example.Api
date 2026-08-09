namespace RedShirt.Example.Api.Common.Exceptions.Responses;

/// <summary>
///     Used within services to abort execution; mapped to an HTTP 409 ProblemDetails response by the API exception
///     handler.
/// </summary>
/// <param name="message"></param>
public class ConflictException(string message) : Exception(message);