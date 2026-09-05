using Amazon.IoT;
using Amazon.IoT.Model;
using Microsoft.Extensions.Options;
using RedShirt.Example.Api.ClientEvents.Library.Core.Exceptions;
using RedShirt.Example.Api.ClientEvents.Library.MqttImplementation.Models;

namespace RedShirt.Example.Api.ClientEvents.Library.MqttImplementation.Services;

/// <summary>
///     Resolves an MQTT broker connection target from AWS IoT Core <c>DescribeEndpoint</c>.
/// </summary>
internal sealed class MqttBrokerUrlResolver(
    IAmazonIoT amazonIoT,
    IOptions<MqttBrokerUrlResolver.ConfigurationModel> configuration) : IMqttBrokerUrlResolver
{
    private const string DefaultWebSocketPath = "/mqtt";

    public async Task<MqttBrokerTarget> ResolveBrokerUrlAsync(CancellationToken cancellationToken)
    {
        var describeResponse = await amazonIoT.DescribeEndpointAsync(new DescribeEndpointRequest
        {
            EndpointType = "iot:Data-ATS"
        }, cancellationToken);

        if (string.IsNullOrWhiteSpace(describeResponse.EndpointAddress))
        {
            throw new ApiClientEventsException("IoT DescribeEndpoint did not return an endpoint address.")
            {
                CouldBeTransient = true,
                IsHandled = false
            };
        }

        var endpointAddress = describeResponse.EndpointAddress;
        var endpointUri = BuildUriFromHostPort(endpointAddress, DefaultWebSocketPath);
        var brokerConnectHost = configuration.Value.BrokerConnectHost;
        var connectHost = string.IsNullOrWhiteSpace(brokerConnectHost)
            ? endpointUri.Host
            : brokerConnectHost;

        var brokerUrl = new UriBuilder(endpointUri.Scheme, connectHost, endpointUri.Port, endpointUri.AbsolutePath)
            .Uri
            .ToString();

        var needsHostHeader = !connectHost.Equals(endpointUri.Host, StringComparison.OrdinalIgnoreCase);
        return new MqttBrokerTarget
        {
            BrokerUrl = brokerUrl,
            WebSocketHostHeader = needsHostHeader ? endpointAddress : null
        };
    }

    private static Uri BuildUriFromHostPort(string hostPort, string path)
    {
        if (Uri.TryCreate($"ws://{hostPort}{path}", UriKind.Absolute, out var uri))
        {
            return uri;
        }

        throw new ApiClientEventsException($"IoT endpoint address '{hostPort}' is not a valid host:port value.")
        {
            CouldBeTransient = false,
            IsHandled = false
        };
    }

    internal sealed class ConfigurationModel
    {
        /// <summary>
        ///     Optional TCP/WebSocket connect host when resolving via IoT <c>DescribeEndpoint</c>.
        ///     Use this in Docker when the IoT endpoint hostname (for example <c>*.localhost</c>) does not resolve to the
        ///     MiniStack container; the resolver connects to this host and sends the DescribeEndpoint value as the HTTP
        ///     <c>Host</c> header.
        /// </summary>
        public string? BrokerConnectHost { get; init; }
    }
}
