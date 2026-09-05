using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RedShirt.Example.Api.ClientEvents.Library.Core.Exceptions;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Factories;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Models;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Services;
using RedShirt.Example.Api.Common.SecretManagers.Core.Services;

namespace RedShirt.Example.Api.ClientEvents.Library.Mqtt.UnitTests.Tests.Factories;

public class ApiMqttClientFactoryTests
{
    private static ApiMqttClientFactory CreateSut(
        ApiMqttClientFactory.ConfigurationModel configuration,
        Mock<IMqttBrokerUrlResolver>? brokerUrlResolver = null,
        Mock<ISecretManagerService>? secretManager = null)
    {
        brokerUrlResolver ??= new Mock<IMqttBrokerUrlResolver>(MockBehavior.Strict);
        secretManager ??= new Mock<ISecretManagerService>(MockBehavior.Strict);

        return new ApiMqttClientFactory(
            Options.Create(configuration),
            secretManager.Object,
            brokerUrlResolver.Object,
            NullLogger<ApiMqttClientFactory>.Instance);
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
}