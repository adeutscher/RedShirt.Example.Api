namespace RedShirt.Example.Api.Common.Exceptions.Responses;

/// <summary>
///     Used within services to abort execution; mapped to an HTTP 404 ProblemDetails response by the API exception
///     handler.
/// </summary>
public class ResourceNotFoundException() : Exception(string.Empty);
