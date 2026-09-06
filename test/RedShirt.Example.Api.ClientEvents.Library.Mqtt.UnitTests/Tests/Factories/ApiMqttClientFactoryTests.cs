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
using System.Reflection;

namespace RedShirt.Example.Api.ClientEvents.Library.Mqtt.UnitTests.Tests.Factories;

public class ApiMqttClientFactoryTests
{
    private static ApiMqttClientFactory CreateSut(
        ApiMqttClientFactory.ConfigurationModel configuration,
        Mock<IMqttBrokerUrlResolver>? brokerUrlResolver = null,
        Mock<ISecretManagerService>? secretManager = null,
        ILogger<ApiMqttClientFactory>? logger = null)
    {
        brokerUrlResolver ??= new Mock<IMqttBrokerUrlResolver>(MockBehavior.Strict);
        secretManager ??= new Mock<ISecretManagerService>(MockBehavior.Strict);

        return new ApiMqttClientFactory(
            Options.Create(configuration),
            secretManager.Object,
            brokerUrlResolver.Object,
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
                    state.ToString()!.Contains(expectedSubstring, StringComparison.Ordinal)),
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