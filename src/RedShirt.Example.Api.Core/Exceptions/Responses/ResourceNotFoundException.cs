namespace RedShirt.Example.Api.Core.Exceptions.Responses;

/// <summary>
///     Used within services to abort execution and trigger an HTTP 404 response at the endpoint level.
/// </summary>
/// <param name="message"></param>
public class ResourceNotFoundException : Exception;