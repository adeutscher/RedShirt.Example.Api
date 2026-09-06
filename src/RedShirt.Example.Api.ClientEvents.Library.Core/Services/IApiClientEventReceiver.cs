using RedShirt.Example.Api.ClientEvents.Library.Core.Models;

namespace RedShirt.Example.Api.ClientEvents.Library.Core.Services;

/// <summary>
///     Streams typed client events. Implementations are expected to use MQTT as the transport;
///     topic names should be MQTT-shaped (for example <c>domain/entity/id</c>).
/// </summary>
/// <typeparam name="TPayload">Event body type.</typeparam>
public interface IApiClientEventReceiver<TPayload>
{
    /// <summary>
    ///     Streams client events from the transport. When <paramref name="topics" /> is <c>null</c> or empty,
    ///     no topic filter is applied. Otherwise only events published on the listed MQTT-shaped topics are yielded.
    /// </summary>
    /// <param name="topics">Optional MQTT topic filter list.</param>
    /// <param name="cancellationToken">Token used to cancel the stream.</param>
    IAsyncEnumerable<ApiClientEventReceived<TPayload>> ReceiveAsync(
        IReadOnlyList<string>? topics = null,
        CancellationToken cancellationToken = default);
}