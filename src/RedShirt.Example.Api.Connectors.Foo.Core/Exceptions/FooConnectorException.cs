namespace RedShirt.Example.Api.Connectors.Foo.Core.Exceptions;

/// <summary>
///     Classified failure from a Foo connector operation. Thrown by the connector implementation after
///     retry/arbitration so callers can react to a stable, already-handled outcome.
/// </summary>
public sealed class FooConnectorException : Exception
{
    /// <summary>
    ///     When <c>true</c>, a retry wrapper inside the connector layer has already exhausted retries for the
    ///     underlying cause; outer retry layers should not retry again.
    /// </summary>
    public required bool IsHandled { get; init; }

    /// <summary>
    ///     When <c>true</c>, suggests a possible transient or environmental cause could be resolved outside the application
    ///     process (with an infrastructure change, for example) without restarting the application.
    /// </summary>
    public required bool CouldBeTransient { get; init; }

    /// <summary>
    ///     When <c>true</c>, suggests a possible environmental cause that could be resolved outside the application
    ///     process (for example an infrastructure change) without restarting the application.
    /// </summary>
    public required bool CouldBeExternallySolvable { get; init; }

    /// <summary>
    ///     When <c>true</c>, the dependency is limiting how often this application may call it.
    /// </summary>
    public bool IsThrottled { get; init; }

    /// <summary>
    ///     How long the dependency asked callers to wait before trying again, or null when it gave no usable hint.
    /// </summary>
    public TimeSpan? RetryAfter { get; init; }

    public FooConnectorException(Exception innerException) : base(innerException.Message, innerException)
    {
    }

    public FooConnectorException(string message) : base(message)
    {
    }
}