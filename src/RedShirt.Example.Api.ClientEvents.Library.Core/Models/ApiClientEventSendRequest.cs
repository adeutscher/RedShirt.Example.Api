namespace RedShirt.Example.Api.ClientEvents.Library.Core.Models;

/// <summary>
///     Payload and optional routing metadata for a client event publish operation.
/// </summary>
/// <typeparam name="TPayload">Event body type.</typeparam>
public sealed class ApiClientEventSendRequest<TPayload>
{
    public required TPayload Payload { get; init; }

    /// <summary>
    ///     Optional MQTT topic. When omitted, the implementation applies its default routing.
    /// </summary>
    public string? Topic { get; init; }
}
