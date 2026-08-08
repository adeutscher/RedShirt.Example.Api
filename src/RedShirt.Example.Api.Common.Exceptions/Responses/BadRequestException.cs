namespace RedShirt.Example.Api.Common.Exceptions.Responses;

/// <summary>
///     Used within services to abort execution; mapped to an HTTP 400 ProblemDetails response by the API exception
///     handler.
/// </summary>
/// <param name="message"></param>
public class BadRequestException(string message) : Exception(message);
