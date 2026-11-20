using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using MQTTnet;
using RedShirt.Example.Api.ClientEvents.Library.Core.Models;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Factories;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Services;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Services.Resilience;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace RedShirt.Example.Api.ClientEvents.Library.Mqtt.UnitTests.Tests.Services;

public class MqttApiClientEventReceiverTests
{
    private const string Topic = "example-message/user/user-1";

    private readonly CapturingLogger _logger = new();

    private MqttApiClientEventReceiver<TestPayload> Receiver(int? maxPayloadBytes)
    {
        return new MqttApiClientEventReceiver<TestPayload>(
            new Mock<IMqttClientFactory>(MockBehavior.Strict).Object,
            new Mock<IMqttClientEventsRetryWrapperService>(MockBehavior.Strict).Object,
            Options.Create(new MqttApiClientEventReceiverConfiguration {MaxPayloadBytes = maxPayloadBytes}),
            _logger);
    }

    /// <summary>
    ///     A valid JSON payload of exactly <paramref name="sizeBytes" /> bytes, so a drop can only come from the size
    ///     check and not from a deserialization failure.
    /// </summary>
    private static MqttApplicationMessage MessageOfSize(int sizeBytes)
    {
        var overhead = Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(new TestPayload("")));
        var json = JsonSerializer.Serialize(new TestPayload(new string('x', sizeBytes - overhead)));
        Assert.Equal(sizeBytes, Encoding.UTF8.GetByteCount(json));

        return new MqttApplicationMessageBuilder()
            .WithTopic(Topic)
            .WithPayload(json)
            .Build();
    }

    private static async Task<List<ApiClientEventReceived<TestPayload>>> Handle(
        MqttApiClientEventReceiver<TestPayload> receiver, MqttApplicationMessage message)
    {
        var channel = Channel.CreateUnbounded<ApiClientEventReceived<TestPayload>>();
        await receiver.HandleApplicationMessageAsync(message, channel.Writer, TestContext.Current.CancellationToken);
        channel.Writer.Complete();

        var received = new List<ApiClientEventReceived<TestPayload>>();
        await foreach (var item in channel.Reader.ReadAllAsync(TestContext.Current.CancellationToken))
        {
            received.Add(item);
        }

        return received;
    }

    private bool LoggedDrop()
    {
        return _logger.Entries.Any(entry =>
            entry.Level == LogLevel.Warning && entry.Message.StartsWith("Dropped MQTT client event payload"));
    }

    [Theory]
    [InlineData(1_000)]
    [InlineData(1_024)]
    public async Task HandleApplicationMessageAsync_AtOrUnderLimit_IsDelivered(int sizeBytes)
    {
        var received = await Handle(Receiver(1_024), MessageOfSize(sizeBytes));

        var item = Assert.Single(received);
        Assert.Equal(Topic, item.Topic);
        Assert.False(LoggedDrop());
    }

    [Fact]
    public async Task HandleApplicationMessageAsync_OverLimit_IsDroppedAndLogged()
    {
        var received = await Handle(Receiver(1_024), MessageOfSize(1_025));

        Assert.Empty(received);
        Assert.True(LoggedDrop());
    }

    [Fact]
    public async Task HandleApplicationMessageAsync_Unset_UsesDefaultLimit()
    {
        var receiver = Receiver(null);

        Assert.Single(await Handle(receiver,
            MessageOfSize(MqttApiClientEventReceiverConfiguration.DefaultMaxPayloadBytes)));
        Assert.Empty(await Handle(receiver,
            MessageOfSize(MqttApiClientEventReceiverConfiguration.DefaultMaxPayloadBytes + 1)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task HandleApplicationMessageAsync_ZeroOrNegative_DeliversAnySize(int maxPayloadBytes)
    {
        var received = await Handle(Receiver(maxPayloadBytes),
            MessageOfSize(MqttApiClientEventReceiverConfiguration.DefaultMaxPayloadBytes * 2));

        Assert.Single(received);
        Assert.False(LoggedDrop());
    }

    [Fact]
    public async Task HandleApplicationMessageAsync_AfterDrop_LaterMessagesStillArrive()
    {
        var receiver = Receiver(1_024);
        var channel = Channel.CreateUnbounded<ApiClientEventReceived<TestPayload>>();

        await receiver.HandleApplicationMessageAsync(MessageOfSize(4_096), channel.Writer,
            TestContext.Current.CancellationToken);
        await receiver.HandleApplicationMessageAsync(MessageOfSize(100), channel.Writer,
            TestContext.Current.CancellationToken);

        Assert.True(channel.Reader.TryRead(out _));
        Assert.False(channel.Reader.TryRead(out _));
    }

    [Theory]
    [InlineData(null, MqttApiClientEventReceiverConfiguration.DefaultMaxPayloadBytes)]
    [InlineData(4_096, 4_096)]
    [InlineData(0, null)]
    [InlineData(-5, null)]
    public void EffectiveMaxPayloadBytes(int? configured, int? expected)
    {
        Assert.Equal(expected,
            new MqttApiClientEventReceiverConfiguration {MaxPayloadBytes = configured}.EffectiveMaxPayloadBytes);
    }

    public sealed record TestPayload(string Message);

    private sealed class CapturingLogger : ILogger<MqttApiClientEventReceiver<TestPayload>>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add((logLevel, formatter(state, exception)));
        }
    }
}
