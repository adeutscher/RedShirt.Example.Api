using Amazon.IoT;
using Amazon.IoT.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Formatter;
using RedShirt.Example.Api.ClientEvents.Library.Core.Exceptions;
using RedShirt.Example.Api.Common.SecretManagers.Core.Services;
using System.Net.Sockets;

namespace RedShirt.Example.Api.ClientEvents.Library.MqttImplementation.Factories;

internal interface IMqttClientFactory
{
    Task<IMqttClient> CreateConnectedClientAsync(CancellationToken cancellationToken);
}

internal sealed class ApiMqttClientFactory(
    IOptions<ApiMqttClientFactory.ConfigurationModel> configuration,
    ISecretManagerService secretManagerService,
    IAmazonIoT amazonIoT,
    ILogger<ApiMqttClientFactory> logger) : IMqttClientFactory
{
    private const string DefaultClientIdPrefix = "RedShirt.Example.Api";
    private const string DefaultWebSocketPath = "/mqtt";
    private const string MqttWebSocketSubProtocol = "mqtt";
    private const int DefaultMqttTcpPort = 1883;

    private static readonly string ServerHostname =
        string.IsNullOrWhiteSpace(Environment.MachineName) ? "unknown" : Environment.MachineName;

    private async Task<MqttBrokerTarget> ResolveBrokerTargetAsync(ConfigurationModel config,
        CancellationToken cancellationToken)
    {
        if (!config.ResolveBrokerAddressFromDescribeEndpoint)
        {
            return new MqttBrokerTarget
            {
                BrokerUrl = config.BrokerUrl
            };
        }

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
        var connectHost = string.IsNullOrWhiteSpace(config.BrokerConnectHost)
            ? endpointUri.Host
            : config.BrokerConnectHost;

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

    /// <summary>
    ///     Builds a fully configured <see cref="MqttClientOptionsBuilder" /> for connecting to the MQTT broker.
    /// </summary>
    /// <remarks>
    ///     MQTTnet does not connect from a single opaque URL string. Each transport must be configured explicitly
    ///     (<see cref="MqttClientOptionsBuilder.WithTcpServer(string,int?, AddressFamily)" /> for native MQTT,
    ///     <see cref="MqttClientOptionsBuilder.WithWebSocketServer(Action{MqttClientWebSocketOptionsBuilder})" />
    ///     for MQTT-over-WebSocket). Deployment environments also use different schemes for the same broker
    ///     (for example <c>ws://</c> against MiniStack locally and <c>mqtts://</c> in production), so broker URLs
    ///     are normalized into the builder call the transport requires.
    ///     MQTTnet WebSocket transport expects a full <c>ws://</c> or <c>wss://</c> URI (including the
    ///     <c>/mqtt</c> path and <c>mqtt</c> subprotocol). MiniStack (and AWS IoT) route upgrades by the IoT
    ///     endpoint hostname returned from <c>DescribeEndpoint</c>, not a bare gateway host name.
    ///     MiniStack and AWS IoT data-plane brokers speak MQTT 3.1.1 only.
    /// </remarks>
    private static MqttClientOptionsBuilder GetOptionsBuilder(
        ConfigurationModel config,
        MqttBrokerTarget brokerTarget,
        MqttBrokerCredentials? credentials)
    {
        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithClientId($"{config.EffectiveClientIdPrefix}/{ServerHostname}/{Guid.NewGuid():N}")
            .WithCleanSession();

        if (!string.IsNullOrWhiteSpace(config.ProtocolVersion) &&
            Enum.TryParse<MqttProtocolVersion>(config.ProtocolVersion, out var protocolVersion))
        {
            optionsBuilder.WithProtocolVersion(protocolVersion);
        }

        if (!Uri.TryCreate(brokerTarget.BrokerUrl!, UriKind.Absolute, out var uri))
        {
            optionsBuilder.WithTcpServer(brokerTarget.BrokerUrl!);
        }
        else if (uri.Scheme.Equals("ws", StringComparison.OrdinalIgnoreCase)
                 || uri.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase))
        {
            var webSocketUri = BuildWebSocketUri(uri);
            optionsBuilder.WithWebSocketServer(webSocketBuilder =>
            {
                webSocketBuilder
                    .WithUri(webSocketUri)
                    .WithSubProtocols([MqttWebSocketSubProtocol]);

                if (!string.IsNullOrWhiteSpace(brokerTarget.WebSocketHostHeader))
                {
                    webSocketBuilder.WithRequestHeaders(new Dictionary<string, string>
                    {
                        ["Host"] = brokerTarget.WebSocketHostHeader
                    });
                }
            });
        }
        else if (uri.Scheme.Equals("tcp", StringComparison.OrdinalIgnoreCase)
                 || uri.Scheme.Equals("mqtt", StringComparison.OrdinalIgnoreCase)
                 || uri.Scheme.Equals("mqtts", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.WithTcpServer(uri.Host, uri.IsDefaultPort ? DefaultMqttTcpPort : uri.Port);
        }
        else
        {
            throw new ApiClientEventsException($"Unsupported MQTT broker URL scheme '{uri.Scheme}'.")
            {
                CouldBeTransient = false,
                IsHandled = false
            };
        }

        if (credentials is not null)
        {
            optionsBuilder.WithCredentials(credentials.Username, credentials.Password);
        }

        return optionsBuilder;
    }

    private static string BuildWebSocketUri(Uri uri)
    {
        var path = string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/"
            ? DefaultWebSocketPath
            : uri.AbsolutePath;

        return new UriBuilder(uri.Scheme, uri.Host, uri.Port, path).Uri.ToString();
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

    private async Task<MqttBrokerCredentials?> TryResolveCredentialsAsync(ConfigurationModel config,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(config.UsernameSecretPath)
            || string.IsNullOrWhiteSpace(config.PasswordSecretPath))
        {
            return null;
        }

        var secrets = await secretManagerService.GetSecretsAsync(
            [config.UsernameSecretPath, config.PasswordSecretPath],
            cancellationToken);

        if (!secrets.TryGetValue(config.UsernameSecretPath, out var username)
            || !secrets.TryGetValue(config.PasswordSecretPath, out var password))
        {
            throw new ApiClientEventsException("MQTT username or password secret could not be resolved.")
            {
                CouldBeTransient = false,
                IsHandled = false
            };
        }

        return new MqttBrokerCredentials
        {
            Username = username,
            Password = password
        };
    }

    public async Task<IMqttClient> CreateConnectedClientAsync(CancellationToken cancellationToken)
    {
        var config = configuration.Value;
        var brokerTarget = await ResolveBrokerTargetAsync(config, cancellationToken);
        if (string.IsNullOrWhiteSpace(brokerTarget.BrokerUrl))
        {
            throw new ApiClientEventsException("ClientEvents MQTT broker URL is not configured.")
            {
                CouldBeTransient = false,
                IsHandled = false
            };
        }

        var mqttFactory = new MqttClientFactory();
        var client = mqttFactory.CreateMqttClient();
        var credentials = await TryResolveCredentialsAsync(config, cancellationToken);
        var options = GetOptionsBuilder(config, brokerTarget, credentials).Build();

        try
        {
            var connectResult = await client.ConnectAsync(options, cancellationToken);
            if (connectResult.ResultCode != MqttClientConnectResultCode.Success)
            {
                throw new ApiClientEventsException(
                    $"MQTT broker connection failed with result code {connectResult.ResultCode}.")
                {
                    CouldBeTransient = true,
                    IsHandled = false
                };
            }
        }
        catch (ApiClientEventsException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to connect to MQTT broker at {BrokerUrl}", brokerTarget.BrokerUrl);
            throw new ApiClientEventsException(exception)
            {
                CouldBeTransient = true,
                IsHandled = false
            };
        }

        return client;
    }

    private sealed class MqttBrokerTarget
    {
        public string? BrokerUrl { get; init; }

        public string? WebSocketHostHeader { get; init; }
    }

    private sealed class MqttBrokerCredentials
    {
        public required string Username { get; init; }

        public required string Password { get; init; }
    }

    internal sealed class ConfigurationModel
    {
        /// <summary>
        ///     Prefix for MQTT client ids ({prefix}/{hostname}/{guid}). When null or whitespace,
        ///     <see cref="DefaultClientIdPrefix" /> is used.
        /// </summary>
        public string? ClientIdPrefix { get; init; }

        public string? BrokerUrl { get; init; }

        /// <summary>
        ///     When true, resolves the WebSocket broker address via IoT <c>DescribeEndpoint</c> instead of using
        ///     <see cref="BrokerUrl" /> verbatim. Required for MiniStack and AWS IoT WebSocket clients.
        /// </summary>
        public bool ResolveBrokerAddressFromDescribeEndpoint { get; init; }

        /// <summary>
        ///     Optional TCP/WebSocket connect host when <see cref="ResolveBrokerAddressFromDescribeEndpoint" /> is enabled.
        ///     Use this in Docker when the IoT endpoint hostname (for example <c>*.localhost</c>) does not resolve to the
        ///     MiniStack container; the factory connects to this host and sends the DescribeEndpoint value as the HTTP
        ///     <c>Host</c> header.
        /// </summary>
        public string? BrokerConnectHost { get; init; }

        /// <summary>
        ///     MQTT protocol version to use when connecting. Expected to resolve to a
        ///     <see cref="MQTTnet.Formatter.MqttProtocolVersion" /> value (for example <c>V311</c> or <c>V500</c>).
        /// </summary>
        public string? ProtocolVersion { get; init; }

        public string? UsernameSecretPath { get; init; }

        public string? PasswordSecretPath { get; init; }

        public string EffectiveClientIdPrefix =>
            string.IsNullOrWhiteSpace(ClientIdPrefix) ? DefaultClientIdPrefix : ClientIdPrefix;
    }
}