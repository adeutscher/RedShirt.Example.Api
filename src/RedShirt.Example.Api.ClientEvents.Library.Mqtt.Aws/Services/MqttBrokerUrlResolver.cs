using Amazon.IoT;
using Amazon.IoT.Model;
using Microsoft.Extensions.Options;
using RedShirt.Example.Api.ClientEvents.Library.Core.Exceptions;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Models;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Services;

namespace RedShirt.Example.Api.ClientEvents.Library.Mqtt.Aws.Services;

/// <summary>
///     AWS IoT implementation of <see cref="IMqttBrokerUrlResolver" /> that discovers the broker connection target
///     from IoT Core <c>DescribeEndpoint</c> at runtime.
/// </summary>
/// <remarks>
///     <para>
///         <b>Runtime resolution is optional.</b> The MQTT client factory uses this resolver only when
///         <c>ClientEvents:Mqtt:ResolveBrokerAddressFromDescribeEndpoint</c> is <c>true</c>. In a deployed AWS
///         environment you can set that flag to <c>false</c> and supply the IoT data-plane endpoint as
///         <c>ClientEvents:Mqtt:BrokerUrl</c> instead (for example from IaC output, SSM, or an environment variable).
///         The hostname returned by <c>DescribeEndpoint</c> (<c>iot:Data-ATS</c>) is stable for a given account and
///         region, so a configured <c>BrokerUrl</c> is equivalent to what this resolver would discover on startup.
///         Runtime resolution is a convenience that avoids plumbing the endpoint through configuration; it is not
///         required for production connectivity when DNS resolves to IoT Core directly.
///     </para>
///     <para>
///         <b>When resolution is enabled in real AWS:</b> calling IoT <c>DescribeEndpoint</c> obtains the
///         account-specific data-plane hostname and builds a WebSocket broker URL from it. With
///         <see cref="ConfigurationModel.BrokerConnectHost" /> unset, the resolver connects directly to that hostname,
///         which matches the normal production path described above.
///     </para>
///     <para>
///         <b>Local Docker / MiniStack only:</b> <see cref="ConfigurationModel.BrokerConnectHost" /> and the optional
///         HTTP <c>Host</c> header override. Inside Docker Compose the DescribeEndpoint hostname often does not resolve to
///         the LocalStack/MiniStack container, so a static <c>BrokerUrl</c> alone is insufficient — TCP must connect to a
///         configured host (for example <c>ministack</c>) while the upgrade still advertises the IoT endpoint hostname.
///         This split connect/host behaviour is what makes runtime resolution (or equivalent manual wiring) necessary
///         locally but not in AWS.
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
        ///     Intended for local Docker / MiniStack testing only, when
        ///     <c>ResolveBrokerAddressFromDescribeEndpoint</c> is <c>true</c>. Leave unset in real AWS, where the
        ///     DescribeEndpoint hostname is both the advertised and connectable target. When set, the resolver
        ///     TCP-connects to this host and sends the DescribeEndpoint address as the HTTP <c>Host</c> header (for
        ///     example when <c>*.localhost</c> or <c>*.ministack</c> does not resolve to the MiniStack container from
        ///     the API container). Has no effect when runtime resolution is disabled and
        ///     <c>ClientEvents:Mqtt:BrokerUrl</c> is used instead.
        /// </remarks>
        public string? BrokerConnectHost { get; init; }
    }
}