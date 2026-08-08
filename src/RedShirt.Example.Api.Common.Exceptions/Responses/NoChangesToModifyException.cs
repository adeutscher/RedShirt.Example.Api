namespace RedShirt.Example.Api.Common.Exceptions.Responses;

/// <summary>
///     Used within services to abort execution when a modify operation would not change state; mapped to an HTTP 304
///     ProblemDetails response by the API exception handler.
/// </summary>
public class NoChangesToModifyException() : Exception(string.Empty);
