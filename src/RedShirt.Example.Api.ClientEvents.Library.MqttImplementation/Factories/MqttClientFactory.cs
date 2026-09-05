using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Formatter;
using RedShirt.Example.Api.ClientEvents.Library.Core.Exceptions;
using RedShirt.Example.Api.ClientEvents.Library.MqttImplementation.Models;
using RedShirt.Example.Api.ClientEvents.Library.MqttImplementation.Services;
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
    IMqttBrokerUrlResolver mqttBrokerUrlResolver,
    ILogger<ApiMqttClientFactory> logger) : IMqttClientFactory
{
    private const string DefaultClientIdPrefix = "RedShirt.Example.Api";
    private const string DefaultWebSocketPath = "/mqtt";
    private const string MqttWebSocketSubProtocol = "mqtt";
    private const int DefaultMqttTcpPort = 1883;

    private static readonly string ServerHostname =
        string.IsNullOrWhiteSpace(Environment.MachineName) ? "unknown" : Environment.MachineName;

    /// <summary>
    ///     Try to resolve broker URI and then validate.
    /// </summary>
    /// <remarks>
    ///     Farms out the actual resolution to <see cref="ResolveBrokerTargetInnerAsync" />, the main thing that makes this
    ///     method special is centralized validation.
    /// </remarks>
    /// <param name="config"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ApiClientEventsException"></exception>
    private async Task<MqttBrokerTarget> ResolveBrokerTargetAsync(ConfigurationModel config,
        CancellationToken cancellationToken)
    {
        var brokerTarget = await ResolveBrokerTargetInnerAsync(config, cancellationToken);
        if (string.IsNullOrWhiteSpace(brokerTarget.BrokerUrl))
        {
            throw new ApiClientEventsException("ClientEvents MQTT broker URL is not configured.")
            {
                CouldBeTransient = false,
                IsHandled = false
            };
        }

        return brokerTarget;
    }

    /// <summary>
    ///     Try to resolve broker URL.
    ///     If <see cref="ConfigurationModel.ResolveBrokerAddressFromDescribeEndpoint"/> is <c>false</c>, then use configured broker URL.
    ///     If <see cref="ConfigurationModel.ResolveBrokerAddressFromDescribeEndpoint"/> is <c>true</c>, then use
    ///     <see cref="IMqttBrokerUrlResolver" />.
    /// </summary>
    /// <param name="config"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ApiClientEventsException"></exception>
    private async Task<MqttBrokerTarget> ResolveBrokerTargetInnerAsync(ConfigurationModel config,
        CancellationToken cancellationToken)
    {
        if (!config.ResolveBrokerAddressFromDescribeEndpoint)
        {
            return new MqttBrokerTarget
            {
                BrokerUrl = config.BrokerUrl
            };
        }

        return await mqttBrokerUrlResolver.ResolveBrokerUrlAsync(cancellationToken);
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
    private async Task<MqttClientOptionsBuildResult> GetOptionsBuilderAsync(ConfigurationModel config,
        CancellationToken cancellationToken)
    {
        var brokerTarget = await ResolveBrokerTargetAsync(config, cancellationToken);

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

        if (await TryResolveCredentialsAsync(config, cancellationToken) is { } credentials)
        {
            optionsBuilder.WithCredentials(credentials.Username, credentials.Password);
        }

        return new MqttClientOptionsBuildResult
        {
            OptionsBuilder = optionsBuilder,
            BrokerUrl = brokerTarget.BrokerUrl!
        };
    }

    private static string BuildWebSocketUri(Uri uri)
    {
        var path = string.IsNullOrEmpty(uri.AbsolutePath) || uri.AbsolutePath == "/"
            ? DefaultWebSocketPath
            : uri.AbsolutePath;

        return new UriBuilder(uri.Scheme, uri.Host, uri.Port, path).Uri.ToString();
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
        var optionsBuild = await GetOptionsBuilderAsync(configuration.Value, cancellationToken);

        var mqttFactory = new MqttClientFactory();
        var client = mqttFactory.CreateMqttClient();

        try
        {
            var connectResult = await client.ConnectAsync(optionsBuild.OptionsBuilder.Build(), cancellationToken);
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
            logger.LogError(exception, "Failed to connect to MQTT broker at {BrokerUrl}", optionsBuild.BrokerUrl);
            throw new ApiClientEventsException(exception)
            {
                CouldBeTransient = true,
                IsHandled = false
            };
        }

        return client;
    }

    private sealed class MqttClientOptionsBuildResult
    {
        /// <summary>
        ///     Configured options object builder.
        /// </summary>
        public required MqttClientOptionsBuilder OptionsBuilder { get; init; }

        /// <summary>
        ///     Also return BrokerUrl for logging purposes.
        /// </summary>
        public required string BrokerUrl { get; init; }
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