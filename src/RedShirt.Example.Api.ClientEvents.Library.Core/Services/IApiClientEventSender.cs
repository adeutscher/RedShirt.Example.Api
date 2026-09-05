using RedShirt.Example.Api.ClientEvents.Library.Core.Models;

namespace RedShirt.Example.Api.ClientEvents.Library.Core.Services;

/// <summary>
///     Publishes typed client events. Implementations are expected to use MQTT as the transport;
///     topic names should be MQTT-shaped (for example <c>domain/entity/id</c>).
/// </summary>
/// <typeparam name="TPayload">Event body type.</typeparam>
public interface IApiClientEventSender<TPayload>
{
    /// <summary>
    ///     Publishes a client event. When <see cref="ApiClientEventSendRequest{TPayload}.Topic" /> is set,
    ///     it should be an MQTT-shaped topic understood by the implementation.
    /// </summary>
    Task SendAsync(ApiClientEventSendRequest<TPayload> request, CancellationToken cancellationToken = default);
}
