namespace RedShirt.Example.Api.ClientEvents.Library.Core.Exceptions;

/// <summary>
///     Classified failure from a client-events operation.
/// </summary>
public sealed class ApiClientEventsException : Exception
{
    /// <summary>
    ///     When <c>true</c>, a retry wrapper inside the client-events layer has already exhausted retries
    ///     for the underlying cause; outer retry layers should not retry again.
    /// </summary>
    public required bool IsHandled { get; init; }

    /// <summary>
    ///     When <c>true</c>, suggests a possible transient or environmental cause could be resolved outside the application
    ///     process (with an infrastructure change, for example) without restarting the application.
    /// </summary>
    public required bool CouldBeTransient { get; init; }

    public ApiClientEventsException(Exception innerException) : base(innerException.Message, innerException)
    {
    }

    public ApiClientEventsException(string message) : base(message)
    {
    }
}