namespace RedShirt.Example.Api.Core.Exceptions.Responses;

/// <summary>
///     Used within services to abort execution and trigger an HTTP 400 response at the endpoint level.
/// </summary>
/// <param name="message"></param>
public class BadRequestException(string message) : Exception(message);