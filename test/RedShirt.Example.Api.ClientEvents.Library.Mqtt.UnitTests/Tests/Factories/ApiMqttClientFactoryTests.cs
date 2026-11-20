using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using MQTTnet;
using MQTTnet.Formatter;
using RedShirt.Example.Api.ClientEvents.Library.Core.Exceptions;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Factories;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Models;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Services;
using RedShirt.Example.Api.Common.SecretManagers.Core.Services;
using System.Net;
using System.Net.Sockets;
using System.Reflection;

namespace RedShirt.Example.Api.ClientEvents.Library.Mqtt.UnitTests.Tests.Factories;

public class ApiMqttClientFactoryTests
{
    private static ApiMqttClientFactory CreateSut(
        ApiMqttClientFactory.ConfigurationModel configuration,
        Mock<IMqttBrokerUrlResolver>? brokerUrlResolver = null,
        Mock<ISecretManagerService>? secretManager = null,
        ILogger<ApiMqttClientFactory>? logger = null,
        string environmentName = "Production")
    {
        brokerUrlResolver ??= new Mock<IMqttBrokerUrlResolver>(MockBehavior.Strict);
        var hostEnvironment = new Mock<IHostEnvironment>();
        hostEnvironment.SetupGet(e => e.EnvironmentName).Returns(environmentName);
        secretManager ??= new Mock<ISecretManagerService>(MockBehavior.Strict);

        return new ApiMqttClientFactory(
            Options.Create(configuration),
            secretManager.Object,
            brokerUrlResolver.Object,
            hostEnvironment.Object,
            logger ?? NullLogger<ApiMqttClientFactory>.Instance);
    }

    private static void VerifyLogWarningContaining(
        Mock<ILogger<ApiMqttClientFactory>> logger,
        string expectedSubstring,
        Times times)
    {
        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) =>
                    state!.ToString()!.Contains(expectedSubstring, StringComparison.Ordinal)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }

    private static async Task<MqttClientOptions> InvokeGetOptionsBuilderAsync(
        ApiMqttClientFactory factory,
        ApiMqttClientFactory.ConfigurationModel sutConfiguration)
    {
        var method = typeof(ApiMqttClientFactory).GetMethod(
            "GetOptionsBuilderAsync",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var invokeResult = method.Invoke(factory, [sutConfiguration, CancellationToken.None]);
        Assert.NotNull(invokeResult);

        var resultTask = (Task) invokeResult;
        await resultTask.ConfigureAwait(false);

        var result = resultTask.GetType().GetProperty("Result")!.GetValue(resultTask);
        Assert.NotNull(result);

        var optionsBuilder = result.GetType().GetProperty("OptionsBuilder")!.GetValue(result);
        Assert.NotNull(optionsBuilder);

        var buildMethod = optionsBuilder.GetType().GetMethod("Build");
        Assert.NotNull(buildMethod);

        var options = buildMethod.Invoke(optionsBuilder, null);
        Assert.IsType<MqttClientOptions>(options);

        return (MqttClientOptions) options;
    }

    /// <summary>
    ///     Connects to a loopback listener with the given broker URL template ({port} is replaced) and returns the first
    ///     byte the client sends. A TLS handshake starts with 0x16, an MQTT CONNECT packet with 0x10, and a WebSocket
    ///     upgrade with 'G' (GET). This checks what actually goes on the wire, not just the configured options.
    /// </summary>
    private static async Task<byte> CaptureFirstByteSentAsync(string brokerUrlTemplate)
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint) listener.LocalEndpoint).Port;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        var sut = CreateSut(new ApiMqttClientFactory.ConfigurationModel
        {
            ResolveBrokerAddressExternally = false,
            BrokerUrl = brokerUrlTemplate.Replace("{port}", port.ToString())
        });

        var connectTask = sut.CreateConnectedClientAsync(timeout.Token);

        using var server = await listener.AcceptTcpClientAsync(timeout.Token);
        var buffer = new byte[1];
        var read = await server.GetStream().ReadAsync(buffer, timeout.Token);
        server.Close();

        // The listener never answers, so the client's connect attempt fails. Only the bytes sent matter here.
        await Assert.ThrowsAnyAsync<Exception>(() => connectTask);

        Assert.Equal(1, read);
        return buffer[0];
    }

    [Theory]
    [InlineData("mqtts://127.0.0.1:{port}", 0x16)]
    [InlineData("wss://127.0.0.1:{port}/mqtt", 0x16)]
    [InlineData("mqtt://127.0.0.1:{port}", 0x10)]
    [InlineData("tcp://127.0.0.1:{port}", 0x10)]
    [InlineData("ws://127.0.0.1:{port}/mqtt", (byte) 'G')]
    public async Task CreateConnectedClientAsync_UsesTlsOnlyForSecureSchemes(string brokerUrlTemplate,
        byte expectedFirstByte)
    {
        Assert.Equal(expectedFirstByte, await CaptureFirstByteSentAsync(brokerUrlTemplate));
    }

    [Fact]
    public async Task CreateConnectedClientAsync_WhenBrokerUrlMissing_ThrowsBeforeConnecting()
    {
        var brokerUrlResolver = new Mock<IMqttBrokerUrlResolver>(MockBehavior.Strict);
        var sut = CreateSut(
            new ApiMqttClientFactory.ConfigurationModel
            {
                ResolveBrokerAddressExternally = false,
                BrokerUrl = null
            },
            brokerUrlResolver);

        var exception = await Assert.ThrowsAsync<ApiClientEventsException>(() =>
            sut.CreateConnectedClientAsync(TestContext.Current.CancellationToken));

        Assert.Contains("broker URL", exception.Message, StringComparison.OrdinalIgnoreCase);
        brokerUrlResolver.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateConnectedClientAsync_WhenCredentialsConfigured_ResolvesSecretsBeforeConnecting()
    {
        var brokerUrlResolver = new Mock<IMqttBrokerUrlResolver>(MockBehavior.Strict);
        var secretManager = new Mock<ISecretManagerService>(MockBehavior.Strict);
        secretManager
            .Setup(s => s.GetSecretsAsync(
                It.Is<List<string>>(paths =>
                    paths.Contains("/mqtt/user") && paths.Contains("/mqtt/pass")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>
            {
                ["/mqtt/user"] = "mqtt-user",
                ["/mqtt/pass"] = "mqtt-pass"
            });

        var sut = CreateSut(
            new ApiMqttClientFactory.ConfigurationModel
            {
                ResolveBrokerAddressExternally = false,
                BrokerUrl = "mqtt://127.0.0.1:1",
                UsernameSecretPath = "/mqtt/user",
                PasswordSecretPath = "/mqtt/pass"
            },
            brokerUrlResolver,
            secretManager);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            sut.CreateConnectedClientAsync(TestContext.Current.CancellationToken));

        secretManager.Verify(
            s => s.GetSecretsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        brokerUrlResolver.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateConnectedClientAsync_WhenProtocolVersionInvalid_LogsWarningAndStillAttemptsConnection()
    {
        var logger = new Mock<ILogger<ApiMqttClientFactory>>();
        var sut = CreateSut(
            new ApiMqttClientFactory.ConfigurationModel
            {
                ResolveBrokerAddressExternally = false,
                BrokerUrl = "mqtt://127.0.0.1:1",
                ProtocolVersion = "not-a-version"
            },
            logger: logger.Object);

        var exception = await Assert.ThrowsAsync<ApiClientEventsException>(() =>
            sut.CreateConnectedClientAsync(TestContext.Current.CancellationToken));

        Assert.NotEqual("not-a-version", exception.Message, StringComparer.Ordinal);
        VerifyLogWarningContaining(logger, "Failed to parse MQTT protocol version", Times.Once());
    }

    [Fact]
    public async Task CreateConnectedClientAsync_WhenProtocolVersionValid_DoesNotLogParseWarning()
    {
        var logger = new Mock<ILogger<ApiMqttClientFactory>>();
        var sut = CreateSut(
            new ApiMqttClientFactory.ConfigurationModel
            {
                ResolveBrokerAddressExternally = false,
                BrokerUrl = "mqtt://127.0.0.1:1",
                ProtocolVersion = "V311"
            },
            logger: logger.Object);

        await Assert.ThrowsAsync<ApiClientEventsException>(() =>
            sut.CreateConnectedClientAsync(TestContext.Current.CancellationToken));

        VerifyLogWarningContaining(logger, "Failed to parse MQTT protocol version", Times.Never());
    }

    [Fact]
    public async Task CreateConnectedClientAsync_WhenResolveExternallyFalse_DoesNotCallResolver()
    {
        var brokerUrlResolver = new Mock<IMqttBrokerUrlResolver>(MockBehavior.Strict);
        var sut = CreateSut(
            new ApiMqttClientFactory.ConfigurationModel
            {
                ResolveBrokerAddressExternally = false,
                BrokerUrl = "mqtt://127.0.0.1:1"
            },
            brokerUrlResolver);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            sut.CreateConnectedClientAsync(TestContext.Current.CancellationToken));

        brokerUrlResolver.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateConnectedClientAsync_WhenResolveExternallyTrue_CallsResolver()
    {
        var brokerUrlResolver = new Mock<IMqttBrokerUrlResolver>(MockBehavior.Strict);
        brokerUrlResolver
            .Setup(r => r.ResolveBrokerUrlAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MqttBrokerTarget
            {
                BrokerUrl = "mqtt://127.0.0.1:1"
            });

        var sut = CreateSut(
            new ApiMqttClientFactory.ConfigurationModel
            {
                ResolveBrokerAddressExternally = true
            },
            brokerUrlResolver);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            sut.CreateConnectedClientAsync(TestContext.Current.CancellationToken));

        brokerUrlResolver.Verify(r => r.ResolveBrokerUrlAsync(It.IsAny<CancellationToken>()), Times.Once);
        brokerUrlResolver.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("mqtts://broker.example", "Production")]
    [InlineData("wss://broker.example/mqtt", "Production")]
    [InlineData("mqtt://broker.example", "Development")]
    [InlineData("ws://broker.example/mqtt", "Development")]
    public async Task GetOptionsBuilderAsync_DoesNotWarn_WhenEncryptedOrInDevelopment(string brokerUrl,
        string environmentName)
    {
        var logger = new Mock<ILogger<ApiMqttClientFactory>>();
        var configuration = new ApiMqttClientFactory.ConfigurationModel
        {
            ResolveBrokerAddressExternally = false,
            BrokerUrl = brokerUrl
        };

        await InvokeGetOptionsBuilderAsync(
            CreateSut(configuration, logger: logger.Object, environmentName: environmentName), configuration);

        VerifyLogWarningContaining(logger, "is not encrypted", Times.Never());
    }

    [Fact]
    public async Task GetOptionsBuilderAsync_PlaintextWarningMentionsCredentials_WhenSent()
    {
        var logger = new Mock<ILogger<ApiMqttClientFactory>>();
        var secretManager = new Mock<ISecretManagerService>();
        secretManager
            .Setup(s => s.GetSecretsAsync(It.IsAny<List<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string> {["/mqtt/user"] = "user", ["/mqtt/pass"] = "pass"});
        var configuration = new ApiMqttClientFactory.ConfigurationModel
        {
            ResolveBrokerAddressExternally = false,
            BrokerUrl = "mqtt://broker.example",
            UsernameSecretPath = "/mqtt/user",
            PasswordSecretPath = "/mqtt/pass"
        };

        await InvokeGetOptionsBuilderAsync(
            CreateSut(configuration, secretManager: secretManager, logger: logger.Object), configuration);

        VerifyLogWarningContaining(logger, "Messages and broker credentials can be read", Times.Once());
    }

    [Theory]
    [InlineData("mqtts://broker.example", 8883)]
    [InlineData("mqtts://broker.example:9883", 9883)]
    [InlineData("mqtt://broker.example", 1883)]
    public async Task GetOptionsBuilderAsync_UsesSchemeDefaultPort(string brokerUrl, int expectedPort)
    {
        var configuration = new ApiMqttClientFactory.ConfigurationModel
        {
            ResolveBrokerAddressExternally = false,
            BrokerUrl = brokerUrl
        };

        var options = await InvokeGetOptionsBuilderAsync(CreateSut(configuration), configuration);

        var tcp = Assert.IsType<MqttClientTcpOptions>(options.ChannelOptions);
        Assert.Equal(expectedPort, Assert.IsType<DnsEndPoint>(tcp.RemoteEndpoint).Port);
        Assert.Equal(brokerUrl.StartsWith("mqtts", StringComparison.Ordinal), tcp.TlsOptions.UseTls);
    }

    [Theory]
    [InlineData("mqtt://broker.example")]
    [InlineData("tcp://broker.example")]
    [InlineData("broker.example")]
    [InlineData("ws://broker.example/mqtt")]
    public async Task GetOptionsBuilderAsync_WarnsOncePerBroker_WhenPlaintextOutsideDevelopment(string brokerUrl)
    {
        var logger = new Mock<ILogger<ApiMqttClientFactory>>();
        var configuration = new ApiMqttClientFactory.ConfigurationModel
        {
            ResolveBrokerAddressExternally = false,
            BrokerUrl = brokerUrl
        };
        var sut = CreateSut(configuration, logger: logger.Object);

        await InvokeGetOptionsBuilderAsync(sut, configuration);
        await InvokeGetOptionsBuilderAsync(sut, configuration);

        VerifyLogWarningContaining(logger, "is not encrypted", Times.Once());
    }

    [Theory]
    [InlineData(null, "RedShirt.Example.Api")]
    [InlineData("", "RedShirt.Example.Api")]
    [InlineData("   ", "RedShirt.Example.Api")]
    [InlineData("My.Custom.Prefix", "My.Custom.Prefix")]
    public async Task GetOptionsBuilderAsync_WhenClientIdPrefixConfigured_UsesExpectedClientIdPrefix(
        string? clientIdPrefix,
        string expectedPrefix)
    {
        var config = new ApiMqttClientFactory.ConfigurationModel
        {
            ResolveBrokerAddressExternally = false,
            BrokerUrl = "mqtt://127.0.0.1:1",
            ClientIdPrefix = clientIdPrefix
        };

        var sut = CreateSut(config);
        var options = await InvokeGetOptionsBuilderAsync(sut, config);

        var serverHostname = string.IsNullOrWhiteSpace(Environment.MachineName)
            ? "unknown"
            : Environment.MachineName;

        Assert.StartsWith($"{expectedPrefix}/{serverHostname}/", options.ClientId);

        var clientIdSegments = options.ClientId.Split('/');
        Assert.Equal(3, clientIdSegments.Length);
        Assert.Matches("^[0-9a-f]{32}$", clientIdSegments[2]);
    }

    [Theory]
    [InlineData(null, MqttProtocolVersion.V500)]
    [InlineData("", MqttProtocolVersion.V500)]
    [InlineData("   ", MqttProtocolVersion.V500)]
    [InlineData("V310", MqttProtocolVersion.V310)]
    [InlineData("V311", MqttProtocolVersion.V311)]
    [InlineData("V500", MqttProtocolVersion.V500)]
    public async Task GetOptionsBuilderAsync_WhenProtocolVersionConfigured_AppliesExpectedMqttProtocolVersion(
        string? protocolVersion,
        MqttProtocolVersion expectedProtocolVersion)
    {
        var config = new ApiMqttClientFactory.ConfigurationModel
        {
            ResolveBrokerAddressExternally = false,
            BrokerUrl = "mqtt://127.0.0.1:1",
            ProtocolVersion = protocolVersion
        };

        var sut = CreateSut(config);
        var options = await InvokeGetOptionsBuilderAsync(sut, config);

        Assert.Equal(expectedProtocolVersion, options.ProtocolVersion);
    }
}