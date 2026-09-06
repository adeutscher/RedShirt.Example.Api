using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RedShirt.Example.Api.ClientEvents.Library.Core.Exceptions;
using RedShirt.Example.Api.ClientEvents.Library.Core.Models;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Factories;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Services;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Services.Resilience;

namespace RedShirt.Example.Api.ClientEvents.Library.Mqtt.UnitTests.Tests.Services;

public class MqttApiClientEventSenderTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendAsync_WhenTopicMissing_ThrowsWithoutCallingDependencies(string? topic)
    {
        var mqttClientFactory = new Mock<IMqttClientFactory>(MockBehavior.Strict);
        var retryWrapper = new Mock<IMqttClientEventsRetryWrapperService>(MockBehavior.Strict);

        var sut = new MqttApiClientEventSender<string>(
            mqttClientFactory.Object,
            retryWrapper.Object,
            NullLogger<MqttApiClientEventSender<string>>.Instance);

        var exception = await Assert.ThrowsAsync<ApiClientEventsException>(() =>
            sut.SendAsync(new ApiClientEventSendRequest<string>
            {
                Topic = topic,
                Payload = "hello"
            }, TestContext.Current.CancellationToken));

        Assert.Contains("topic", exception.Message, StringComparison.OrdinalIgnoreCase);
        mqttClientFactory.VerifyNoOtherCalls();
        retryWrapper.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SendAsync_WhenTopicPresent_DelegatesToRetryWrapper()
    {
        var mqttClientFactory = new Mock<IMqttClientFactory>(MockBehavior.Strict);
        var retryWrapper = new Mock<IMqttClientEventsRetryWrapperService>(MockBehavior.Strict);
        retryWrapper
            .Setup(r => r.RunAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task>, CancellationToken>((func, token) => func(token));

        mqttClientFactory
            .Setup(f => f.CreateConnectedClientAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ApiClientEventsException("connect failed")
            {
                CouldBeTransient = true,
                IsHandled = false
            });

        var sut = new MqttApiClientEventSender<string>(
            mqttClientFactory.Object,
            retryWrapper.Object,
            NullLogger<MqttApiClientEventSender<string>>.Instance);

        await Assert.ThrowsAsync<ApiClientEventsException>(() =>
            sut.SendAsync(new ApiClientEventSendRequest<string>
            {
                Topic = "example-message/user/test",
                Payload = "hello"
            }, TestContext.Current.CancellationToken));

        retryWrapper.Verify(
            r => r.RunAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()),
            Times.Once);
        mqttClientFactory.Verify(f => f.CreateConnectedClientAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}