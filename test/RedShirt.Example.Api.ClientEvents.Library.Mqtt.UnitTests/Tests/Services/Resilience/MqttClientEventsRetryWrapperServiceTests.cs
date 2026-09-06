using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using RedShirt.Example.Api.ClientEvents.Library.Core.Exceptions;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Services.Resilience;
using RedShirt.Example.Api.Common.Services.Utility;
using System.Net.Sockets;
using System.Text.Json;

namespace RedShirt.Example.Api.ClientEvents.Library.Mqtt.UnitTests.Tests.Services.Resilience;

public class MqttClientEventsRetryWrapperServiceTests
{
    private static MqttClientEventsRetryWrapperService CreateSut(
        IMqttClientEventsExceptionArbiterService? arbiter = null,
        ISleepService? sleep = null,
        int? retryCount = 3)
    {
        var sleepMock = new Mock<ISleepService>();
        sleepMock
            .Setup(s => s.DelayAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        return new MqttClientEventsRetryWrapperService(
            arbiter ?? new MqttClientEventsExceptionArbiterService(),
            NullLogger<MqttClientEventsRetryWrapperService>.Instance,
            sleep ?? sleepMock.Object,
            Options.Create(new MqttClientEventsRetryWrapperService.ConfigurationModel
            {
                RetryCount = retryCount
            }));
    }

    [Fact]
    public async Task RunAsync_DoesNotWrap_UnexpectedExceptions()
    {
        var sut = CreateSut();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.RunAsync<int>(_ => throw new InvalidOperationException("surprise"),
                TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RunAsync_PassesThrough_HandledApiClientEventsException()
    {
        var sut = CreateSut();

        var original = new ApiClientEventsException("done")
        {
            IsHandled = true,
            CouldBeTransient = true
        };

        var thrown = await Assert.ThrowsAsync<ApiClientEventsException>(() =>
            sut.RunAsync<int>(_ => throw original, TestContext.Current.CancellationToken));

        Assert.Same(original, thrown);
    }

    [Fact]
    public async Task RunAsync_RetriesTransientFailures_ThenSucceeds()
    {
        var attempts = 0;
        var sleep = new Mock<ISleepService>(MockBehavior.Strict);
        sleep
            .Setup(s => s.DelayAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = CreateSut(sleep: sleep.Object);

        var result = await sut.RunAsync(_ =>
        {
            attempts++;
            if (attempts < 3)
            {
                throw new SocketException();
            }

            return Task.FromResult("ok");
        }, TestContext.Current.CancellationToken);

        Assert.Equal("ok", result);
        Assert.Equal(3, attempts);
        sleep.Verify(s => s.DelayAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RunAsync_ReturnsResult_WhenFuncSucceeds()
    {
        var sut = CreateSut();

        var result = await sut.RunAsync(_ => Task.FromResult(42), TestContext.Current.CancellationToken);

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task RunAsync_WrapsExpectedNonTransient_AsApiClientEventsException()
    {
        var sut = CreateSut();

        var wrapped = await Assert.ThrowsAsync<ApiClientEventsException>(() =>
            sut.RunAsync<int>(_ => throw new JsonException("bad payload"),
                TestContext.Current.CancellationToken));

        Assert.True(wrapped.IsHandled);
        Assert.False(wrapped.CouldBeTransient);
        Assert.IsType<JsonException>(wrapped.InnerException);
    }
}