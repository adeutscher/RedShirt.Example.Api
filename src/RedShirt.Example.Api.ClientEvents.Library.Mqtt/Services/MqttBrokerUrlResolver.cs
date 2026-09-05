using Amazon.IoT;
using Amazon.IoT.Model;
using Microsoft.Extensions.Options;
using RedShirt.Example.Api.ClientEvents.Library.Core.Exceptions;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Models;

namespace RedShirt.Example.Api.ClientEvents.Library.Mqtt.Services;

/// <summary>
///     Resolves an MQTT broker connection target from AWS IoT Core <c>DescribeEndpoint</c>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Reusable in real AWS:</b> calling IoT <c>DescribeEndpoint</c> (<c>iot:Data-ATS</c>) to obtain the
///         account-specific data-plane hostname and building a WebSocket broker URL from it. With
///         <see cref="ConfigurationModel.BrokerConnectHost" /> unset, the resolver connects directly to that hostname,
///         which is the normal production path (or equivalent to configuring the endpoint from IaC output).
///     </para>
///     <para>
///         <b>Local Docker / MiniStack only:</b> <see cref="ConfigurationModel.BrokerConnectHost" /> and the optional
///         HTTP <c>Host</c> header override. Inside Docker Compose the DescribeEndpoint hostname often does not resolve to
///         the LocalStack/MiniStack container, so TCP connects to a configured host (for example <c>ministack</c>) while
///         the upgrade still advertises the IoT endpoint hostname.
///     </para>
///     <para>
///         Production AWS WebSocket clients also require <c>wss://</c> and SigV4 signing on the upgrade; this resolver
///         covers endpoint discovery and URL assembly only.
///     </para>
/// </remarks>
internal sealed class MqttBrokerUrlResolver(
    IAmazonIoT amazonIoT,
    IOptions<MqttBrokerUrlResolver.ConfigurationModel> configuration) : IMqttBrokerUrlResolver
{
    private const string DefaultWebSocketPath = "/mqtt";

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

    internal sealed class ConfigurationModel
    {
        /// <summary>
        ///     Optional TCP/WebSocket connect host when resolving via IoT <c>DescribeEndpoint</c>.
        /// </summary>
        /// <remarks>
        ///     Intended for local Docker / MiniStack testing only. Leave unset in real AWS, where the DescribeEndpoint
        ///     hostname is the connection target. When set, the resolver TCP-connects to this host and sends the
        ///     DescribeEndpoint address as the HTTP <c>Host</c> header (for example when
        ///     <c>*.localhost</c> or <c>*.ministack</c> does not resolve to the MiniStack container from the API
        ///     container).
        /// </remarks>
        public string? BrokerConnectHost { get; init; }
    }
}