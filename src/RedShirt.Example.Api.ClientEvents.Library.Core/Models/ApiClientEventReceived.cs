namespace RedShirt.Example.Api.ClientEvents.Library.Core.Models;

/// <summary>
///     A client event received from the transport layer.
/// </summary>
/// <typeparam name="TPayload">Event body type.</typeparam>
public sealed class ApiClientEventReceived<TPayload>
{
    public required string Topic { get; init; }

    public required TPayload Payload { get; init; }
}