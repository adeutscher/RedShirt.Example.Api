using RedShirt.Example.Api.ClientEvents.Library.MqttImplementation.Models;

namespace RedShirt.Example.Api.ClientEvents.Library.MqttImplementation.Services;

/// <summary>
///     Resolves an MQTT broker connection target when a configured <c>BrokerUrl</c> is not sufficient on its own.
/// </summary>
/// <remarks>
///     Most environments can connect using a static broker URL from configuration. This resolver exists for the special
///     case where the broker address must be discovered at runtime.
///     See <see cref="MqttBrokerUrlResolver" /> for environment-specific behaviour.
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
