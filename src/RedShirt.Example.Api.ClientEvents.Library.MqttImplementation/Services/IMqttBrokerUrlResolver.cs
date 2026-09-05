using RedShirt.Example.Api.ClientEvents.Library.MqttImplementation.Models;

namespace RedShirt.Example.Api.ClientEvents.Library.MqttImplementation.Services;

internal interface IMqttBrokerUrlResolver
{
    Task<MqttBrokerTarget> ResolveBrokerUrlAsync(CancellationToken cancellationToken);
}
