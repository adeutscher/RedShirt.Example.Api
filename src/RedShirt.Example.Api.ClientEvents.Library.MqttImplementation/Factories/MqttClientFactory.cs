using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
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
    ILogger<ApiMqttClientFactory> logger) : IMqttClientFactory
{
    private const string DefaultClientIdPrefix = "RedShirt.Example.Api";

    private static readonly string ServerHostname =
        string.IsNullOrWhiteSpace(Environment.MachineName) ? "unknown" : Environment.MachineName;

    public async Task<IMqttClient> CreateConnectedClientAsync(CancellationToken cancellationToken)
    {
        var config = configuration.Value;
        if (string.IsNullOrWhiteSpace(config.BrokerUrl))
        {
            throw new ApiClientEventsException("ClientEvents MQTT broker URL is not configured.")
            {
                CouldBeTransient = false,
                IsHandled = false
            };
        }

        var mqttFactory = new MqttClientFactory();
        var client = mqttFactory.CreateMqttClient();
        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithClientId($"{config.EffectiveClientIdPrefix}/{ServerHostname}/{Guid.NewGuid():N}")
            .WithCleanSession();

        ApplyBrokerAddress(optionsBuilder, config.BrokerUrl);

        if (await TryResolveCredentialsAsync(config, cancellationToken) is { } credentials)
        {
            optionsBuilder.WithCredentials(credentials.Username, credentials.Password);
        }

        try
        {
            var connectResult = await client.ConnectAsync(optionsBuilder.Build(), cancellationToken);
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
            client.Dispose();
            throw;
        }
        catch (Exception exception)
        {
            client.Dispose();
            logger.LogError(exception, "Failed to connect to MQTT broker at {BrokerUrl}", config.BrokerUrl);
            throw new ApiClientEventsException(exception)
            {
                CouldBeTransient = true,
                IsHandled = false
            };
        }

        return client;
    }

    /// <summary>
    ///     Maps a configured <paramref name="brokerUrl" /> onto <see cref="MqttClientOptionsBuilder" />.
    /// </summary>
    /// <remarks>
    ///     MQTTnet does not connect from a single opaque URL string. Each transport must be configured explicitly
    ///     (<see cref="MqttClientOptionsBuilder.WithTcpServer(string,int?, AddressFamily)" /> for native MQTT,
    ///     <see cref="MqttClientOptionsBuilder.WithWebSocketServer(Action{MqttClientWebSocketOptionsBuilder})" />
    ///     for MQTT-over-WebSocket). Deployment environments also use different schemes for the same broker
    ///     (for example <c>ws://</c> against MiniStack locally and <c>mqtts://</c> in production), so this helper
    ///     normalizes the configured URL into the builder call the transport requires.
    /// </remarks>
    private static void ApplyBrokerAddress(MqttClientOptionsBuilder optionsBuilder, string brokerUrl)
    {
        // Host-only values (no scheme) are treated as a TCP host name for plain MQTT.
        if (!Uri.TryCreate(brokerUrl, UriKind.Absolute, out var uri))
        {
            optionsBuilder.WithTcpServer(brokerUrl);
            return;
        }

        // WebSocket transports: MQTTnet expects "host[:port][/path]" without the ws/wss scheme prefix.
        if (uri.Scheme.Equals("ws", StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals("wss", StringComparison.OrdinalIgnoreCase))
        {
            var webSocketUri = uri.IsDefaultPort
                ? $"{uri.Host}{uri.PathAndQuery}"
                : $"{uri.Host}:{uri.Port}{uri.PathAndQuery}";

            optionsBuilder.WithWebSocketServer(webSocketBuilder =>
                webSocketBuilder.WithUri(webSocketUri));
            return;
        }

        // Native MQTT over TCP/TLS: map mqtt/mqtts/tcp URIs to host and port (default 1883 when omitted).
        // ReSharper disable once InvertIf
        if (uri.Scheme.Equals("tcp", StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals("mqtt", StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals("mqtts", StringComparison.OrdinalIgnoreCase))
        {
            optionsBuilder.WithTcpServer(uri.Host, uri.IsDefaultPort ? 1883 : uri.Port);
            return;
        }

        throw new ApiClientEventsException($"Unsupported MQTT broker URL scheme '{uri.Scheme}'.")
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

        public string? UsernameSecretPath { get; init; }

        public string? PasswordSecretPath { get; init; }

        public string EffectiveClientIdPrefix =>
            string.IsNullOrWhiteSpace(ClientIdPrefix) ? DefaultClientIdPrefix : ClientIdPrefix;
    }
}
