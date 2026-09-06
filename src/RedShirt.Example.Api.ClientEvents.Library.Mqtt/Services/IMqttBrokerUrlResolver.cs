using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Models;

namespace RedShirt.Example.Api.ClientEvents.Library.Mqtt.Services;

/// <summary>
///     Resolves an MQTT broker connection target when a configured <c>BrokerUrl</c> is not sufficient on its own.
/// </summary>
/// <remarks>
///     Most environments can connect using a static broker URL from configuration. This resolver exists for the special
///     case where the broker address must be discovered at runtime — for example via AWS IoT
///     <c>DescribeEndpoint</c> in <c>RedShirt.Example.Api.ClientEvents.Library.Mqtt.Aws</c>.
/// </remarks>
internal interface IMqttBrokerUrlResolver
{
    /// <summary>
    ///     Resolves the broker URL and any transport-specific connection hints needed to reach it.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A broker target suitable for passing to the MQTT client options builder.</returns>
    Task<MqttBrokerTarget> ResolveBrokerUrlAsync(CancellationToken cancellationToken);
}