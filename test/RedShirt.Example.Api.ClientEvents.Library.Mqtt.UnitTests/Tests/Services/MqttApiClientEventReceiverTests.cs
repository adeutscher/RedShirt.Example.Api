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

    private MqttApiClientEventReceiver<TestPayload> Receiver(int? maxPayloadBytes,
        int? maxQueuedEventsPerStream = null)
    {
        return new MqttApiClientEventReceiver<TestPayload>(
            new Mock<IMqttClientFactory>(MockBehavior.Strict).Object,
            new Mock<IMqttClientEventsRetryWrapperService>(MockBehavior.Strict).Object,
            Options.Create(new MqttApiClientEventReceiverConfiguration
            {
                MaxPayloadBytes = maxPayloadBytes,
                MaxQueuedEventsPerStream = maxQueuedEventsPerStream
            }),
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
        var channel = receiver.CreateChannel();
        receiver.HandleApplicationMessage(message, channel.Writer);
        channel.Writer.TryComplete();

        return await ReadAll(channel);
    }

    private static async Task<List<ApiClientEventReceived<TestPayload>>> ReadAll(
        Channel<ApiClientEventReceived<TestPayload>> channel)
    {
        var received = new List<ApiClientEventReceived<TestPayload>>();
        await foreach (var item in channel.Reader.ReadAllAsync(TestContext.Current.CancellationToken))
        {
            received.Add(item);
        }

        return received;
    }

    private static MqttApplicationMessage NumberedMessage(int number)
    {
        return new MqttApplicationMessageBuilder()
            .WithTopic(Topic)
            .WithPayload(JsonSerializer.Serialize(new TestPayload(number.ToString())))
            .Build();
    }

    private int LoggedStreamCloses()
    {
        return _logger.Entries.Count(entry =>
            entry.Level == LogLevel.Warning && entry.Message.StartsWith("Closed MQTT client event stream"));
    }

    private bool LoggedDrop()
    {
        return _logger.Entries.Any(entry =>
            entry.Level == LogLevel.Warning && entry.Message.StartsWith("Dropped MQTT client event payload"));
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

    [Theory]
    [InlineData(null, MqttApiClientEventReceiverConfiguration.DefaultMaxQueuedEventsPerStream)]
    [InlineData(10, 10)]
    [InlineData(0, null)]
    [InlineData(-5, null)]
    public void EffectiveMaxQueuedEventsPerStream(int? configured, int? expected)
    {
        Assert.Equal(expected,
            new MqttApiClientEventReceiverConfiguration {MaxQueuedEventsPerStream = configured}
                .EffectiveMaxQueuedEventsPerStream);
    }

    [Fact]
    public void HandleApplicationMessage_AfterDrop_LaterMessagesStillArrive()
    {
        var receiver = Receiver(1_024);
        var channel = receiver.CreateChannel();

        receiver.HandleApplicationMessage(MessageOfSize(4_096), channel.Writer);
        receiver.HandleApplicationMessage(MessageOfSize(100), channel.Writer);

        Assert.True(channel.Reader.TryRead(out _));
        Assert.False(channel.Reader.TryRead(out _));
    }

    [Fact]
    public void HandleApplicationMessage_AfterStreamEnded_DropsWithoutLoggingAgain()
    {
        var receiver = Receiver(null, 1);
        var channel = receiver.CreateChannel();

        for (var i = 1; i <= 5; i++)
        {
            receiver.HandleApplicationMessage(NumberedMessage(i), channel.Writer);
        }

        Assert.True(channel.Reader.TryRead(out _));
        receiver.HandleApplicationMessage(NumberedMessage(6), channel.Writer);

        Assert.False(channel.Reader.TryRead(out _));
        Assert.True(channel.Reader.Completion.IsCompleted);
        Assert.Equal(1, LoggedStreamCloses());
    }

    [Theory]
    [InlineData(1_000)]
    [InlineData(1_024)]
    public async Task HandleApplicationMessage_AtOrUnderLimit_IsDelivered(int sizeBytes)
    {
        var received = await Handle(Receiver(1_024), MessageOfSize(sizeBytes));

        var item = Assert.Single(received);
        Assert.Equal(Topic, item.Topic);
        Assert.False(LoggedDrop());
    }

    [Fact]
    public async Task HandleApplicationMessage_OverLimit_IsDroppedAndLogged()
    {
        var received = await Handle(Receiver(1_024), MessageOfSize(1_025));

        Assert.Empty(received);
        Assert.True(LoggedDrop());
    }

    [Fact]
    public async Task HandleApplicationMessage_QueueFull_EndsStreamAfterQueuedEvents()
    {
        var receiver = Receiver(null, 3);
        var channel = receiver.CreateChannel();

        for (var i = 1; i <= 4; i++)
        {
            receiver.HandleApplicationMessage(NumberedMessage(i), channel.Writer);
        }

        // The writer completed itself, so reading ends after the queued events without the test completing it.
        var received = await ReadAll(channel);

        Assert.Equal(["1", "2", "3"], received.Select(item => item.Payload.Message));
        Assert.Equal(1, LoggedStreamCloses());
    }

    [Fact]
    public void HandleApplicationMessage_UnsetQueueLimit_UsesDefault()
    {
        var receiver = Receiver(null);
        var channel = receiver.CreateChannel();

        for (var i = 0; i < MqttApiClientEventReceiverConfiguration.DefaultMaxQueuedEventsPerStream; i++)
        {
            receiver.HandleApplicationMessage(NumberedMessage(i), channel.Writer);
        }

        Assert.Equal(0, LoggedStreamCloses());

        receiver.HandleApplicationMessage(NumberedMessage(-1), channel.Writer);

        Assert.Equal(1, LoggedStreamCloses());
    }

    [Fact]
    public async Task HandleApplicationMessage_Unset_UsesDefaultLimit()
    {
        var receiver = Receiver(null);

        Assert.Single(await Handle(receiver,
            MessageOfSize(MqttApiClientEventReceiverConfiguration.DefaultMaxPayloadBytes)));
        Assert.Empty(await Handle(receiver,
            MessageOfSize(MqttApiClientEventReceiverConfiguration.DefaultMaxPayloadBytes + 1)));
    }

    [Fact]
    public async Task HandleApplicationMessage_UpToQueueLimit_AllDeliveredInOrder()
    {
        var receiver = Receiver(null, 3);
        var channel = receiver.CreateChannel();

        for (var i = 1; i <= 3; i++)
        {
            receiver.HandleApplicationMessage(NumberedMessage(i), channel.Writer);
        }

        channel.Writer.TryComplete();
        var received = await ReadAll(channel);

        Assert.Equal(["1", "2", "3"], received.Select(item => item.Payload.Message));
        Assert.Equal(0, LoggedStreamCloses());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task HandleApplicationMessage_ZeroOrNegativeQueueLimit_IsUnbounded(int maxQueuedEventsPerStream)
    {
        var receiver = Receiver(null, maxQueuedEventsPerStream);
        var channel = receiver.CreateChannel();
        const int count = MqttApiClientEventReceiverConfiguration.DefaultMaxQueuedEventsPerStream * 4;

        for (var i = 0; i < count; i++)
        {
            receiver.HandleApplicationMessage(NumberedMessage(i), channel.Writer);
        }

        Assert.False(channel.Reader.Completion.IsCompleted);
        channel.Writer.TryComplete();
        Assert.Equal(count, (await ReadAll(channel)).Count);
        Assert.Equal(0, LoggedStreamCloses());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task HandleApplicationMessage_ZeroOrNegative_DeliversAnySize(int maxPayloadBytes)
    {
        var received = await Handle(Receiver(maxPayloadBytes),
            MessageOfSize(MqttApiClientEventReceiverConfiguration.DefaultMaxPayloadBytes * 2));

        Assert.Single(received);
        Assert.False(LoggedDrop());
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