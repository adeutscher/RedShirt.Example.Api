namespace RedShirt.Api.Example.Connectors.Foo.Implementation.Exceptions;

/// <summary>
///     Classified failure from a Foo connector operation.
/// </summary>
public sealed class ApiFooConnectorException : Exception
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

    public ApiFooConnectorException(Exception innerException) : base(innerException.Message, innerException)
    {
    }

    public ApiFooConnectorException(string message) : base(message)
    {
    }
}
