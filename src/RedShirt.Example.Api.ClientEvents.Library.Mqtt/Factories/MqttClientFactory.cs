using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Formatter;
using RedShirt.Example.Api.ClientEvents.Library.Core.Exceptions;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Models;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Services;
using RedShirt.Example.Api.Common.SecretManagers.Core.Services;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace RedShirt.Example.Api.ClientEvents.Library.Mqtt.Factories;

internal interface IMqttClientFactory
{
    Task<IMqttClient> CreateConnectedClientAsync(CancellationToken cancellationToken);
}

internal sealed class ApiMqttClientFactory(
    IOptions<ApiMqttClientFactory.ConfigurationModel> configuration,
    ISecretManagerService secretManagerService,
    IMqttBrokerUrlResolver mqttBrokerUrlResolver,
    IHostEnvironment hostEnvironment,
    ILogger<ApiMqttClientFactory> logger) : IMqttClientFactory
{
    private const string DefaultClientIdPrefix = "RedShirt.Example.Api";
    private const string DefaultWebSocketPath = "/mqtt";
    private const string MqttWebSocketSubProtocol = "mqtt";
    private const int DefaultMqttTcpPort = 1883;
    private const int DefaultMqttTlsPort = 8883;

    /// <summary>
    ///     Broker URLs already warned about for plaintext transport, so the warning appears once per broker, not once
    ///     per connection.
    /// </summary>
    private readonly ConcurrentDictionary<string, byte> _plaintextWarnedBrokerUrls = new(StringComparer.Ordinal);

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
    ///     Resolves the broker connection target from configuration or an external resolver.
    /// </summary>
    /// <remarks>
    ///     When <see cref="ConfigurationModel.ResolveBrokerAddressExternally" /> is <c>false</c>, returns
    ///     <see cref="ConfigurationModel.BrokerUrl" /> as-is. When <c>true</c>, delegates to
    ///     <see cref="IMqttBrokerUrlResolver" /> (for example the AWS IoT implementation in
    ///     <c>RedShirt.Example.Api.ClientEvents.Library.Mqtt.Aws</c>).
    /// </remarks>
    /// <param name="config"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="ApiClientEventsException"></exception>
    private async Task<MqttBrokerTarget> ResolveBrokerTargetInnerAsync(ConfigurationModel config,
        CancellationToken cancellationToken)
    {
        if (!config.ResolveBrokerAddressExternally)
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

        if (!string.IsNullOrWhiteSpace(config.ProtocolVersion))
        {
            if (Enum.TryParse<MqttProtocolVersion>(config.ProtocolVersion, out var protocolVersion))
            {
                optionsBuilder.WithProtocolVersion(protocolVersion);
            }
            else
            {
                logger.LogWarning("Failed to parse MQTT protocol version {ProtocolVersion} to {EnumName}",
                    config.ProtocolVersion, typeof(MqttProtocolVersion).FullName);
            }
        }

        // Transport security by scheme:
        //   * mqtts:// is MQTT over TLS (default port 8883), with certificate and host name validation.
        //   * wss:// is MQTT over WebSocket over TLS (ClientWebSocket negotiates TLS from the scheme).
        //   * mqtt://, tcp://, ws:// and a bare host[:port] are PLAINTEXT. Credentials and messages are readable on
        //     the network, so outside Development a warning is logged (see WarnIfPlaintext).
        bool isEncrypted;
        if (!Uri.TryCreate(brokerTarget.BrokerUrl!, UriKind.Absolute, out var uri))
        {
            optionsBuilder.WithTcpServer(brokerTarget.BrokerUrl!);
            isEncrypted = false;
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
            isEncrypted = uri.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase);
        }
        else if (uri.Scheme.Equals("mqtts", StringComparison.OrdinalIgnoreCase))
        {
            // TLS with the platform's default certificate and host name validation. There is deliberately no option
            // to skip validation.
            optionsBuilder
                .WithTcpServer(uri.Host, uri.IsDefaultPort ? DefaultMqttTlsPort : uri.Port)
                .WithTlsOptions(tls => tls.UseTls());
            isEncrypted = true;
        }
        else if (uri.Scheme.Equals("tcp", StringComparison.OrdinalIgnoreCase)
                 || uri.Scheme.Equals("mqtt", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.WithTcpServer(uri.Host, uri.IsDefaultPort ? DefaultMqttTcpPort : uri.Port);
            isEncrypted = false;
        }
        else
        {
            throw new ApiClientEventsException($"Unsupported MQTT broker URL scheme '{uri.Scheme}'.")
            {
                CouldBeTransient = false,
                IsHandled = false
            };
        }

        var credentials = await TryResolveCredentialsAsync(config, cancellationToken);
        if (credentials is not null)
        {
            optionsBuilder.WithCredentials(credentials.Username, credentials.Password);
        }

        if (!isEncrypted)
        {
            WarnIfPlaintext(brokerTarget.BrokerUrl!, credentials is not null);
        }

        return new MqttClientOptionsBuildResult
        {
            OptionsBuilder = optionsBuilder,
            BrokerUrl = brokerTarget.BrokerUrl!
        };
    }

    /// <summary>
    ///     Outside Development, logs once per broker URL that the connection is not encrypted.
    /// </summary>
    private void WarnIfPlaintext(string brokerUrl, bool sendsCredentials)
    {
        if (hostEnvironment.IsDevelopment() || !_plaintextWarnedBrokerUrls.TryAdd(brokerUrl, 0))
        {
            return;
        }

        logger.LogWarning(
            "MQTT broker connection to {BrokerUrl} is not encrypted (use mqtts:// or wss://). Messages{CredentialsNote} " +
            "can be read on the network.",
            brokerUrl,
            sendsCredentials ? " and broker credentials" : string.Empty);
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
        ///     When true, resolves the broker address through <see cref="IMqttBrokerUrlResolver" /> instead of using
        ///     <see cref="BrokerUrl" /> verbatim.
        /// </summary>
        /// <remarks>
        ///     Use this when the broker URL cannot be known up front or when the registered resolver supplies
        ///     transport-specific connection metadata (for example a WebSocket <c>Host</c> header). When false,
        ///     <see cref="BrokerUrl" /> must be set to a complete broker address.
        /// </remarks>
        public bool ResolveBrokerAddressExternally { get; init; }

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