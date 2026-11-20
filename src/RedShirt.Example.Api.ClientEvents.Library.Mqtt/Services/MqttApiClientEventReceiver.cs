using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Protocol;
using RedShirt.Example.Api.ClientEvents.Library.Core.Exceptions;
using RedShirt.Example.Api.ClientEvents.Library.Core.Models;
using RedShirt.Example.Api.ClientEvents.Library.Core.Services;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Factories;
using RedShirt.Example.Api.ClientEvents.Library.Mqtt.Services.Resilience;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;

namespace RedShirt.Example.Api.ClientEvents.Library.Mqtt.Services;

internal sealed class MqttApiClientEventReceiver<TPayload>(
    IMqttClientFactory mqttClientFactory,
    IMqttClientEventsRetryWrapperService retryWrapperService,
    IOptions<MqttApiClientEventReceiverConfiguration> options,
    ILogger<MqttApiClientEventReceiver<TPayload>> logger) : IApiClientEventReceiver<TPayload>
{
    private readonly int? _maxPayloadBytes = options.Value.EffectiveMaxPayloadBytes;
    private readonly int? _maxQueuedEvents = options.Value.EffectiveMaxQueuedEventsPerStream;

    private static async Task DisconnectSafelyAsync(IMqttClient client, CancellationToken cancellationToken)
    {
        if (!client.IsConnected)
        {
            return;
        }

        try
        {
            await client.DisconnectAsync(cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Best-effort disconnect during cleanup.
        }
    }

    /// <summary>
    ///     Creates the queue between the MQTT callback and the stream reader. It is bounded unless
    ///     <see cref="MqttApiClientEventReceiverConfiguration.MaxQueuedEventsPerStream" /> turns the bound off.
    /// </summary>
    internal Channel<ApiClientEventReceived<TPayload>> CreateChannel()
    {
        return _maxQueuedEvents is { } capacity
            ? Channel.CreateBounded<ApiClientEventReceived<TPayload>>(new BoundedChannelOptions(capacity)
            {
                SingleReader = true,
                SingleWriter = false,
                // Only TryWrite is used, which never waits. Wait makes it return false when full.
                FullMode = BoundedChannelFullMode.Wait
            })
            : Channel.CreateUnbounded<ApiClientEventReceived<TPayload>>(
                new UnboundedChannelOptions {SingleReader = true, SingleWriter = false});
    }

    /// <summary>
    ///     Turns one broker message into a received event and writes it to <paramref name="writer" />. Messages that
    ///     are too large, fail to deserialize, or deserialize to null are dropped with a log entry. When the queue is
    ///     full, the writer is completed: the reader gets what is already queued and the stream then ends.
    /// </summary>
    internal void HandleApplicationMessage(MqttApplicationMessage message,
        ChannelWriter<ApiClientEventReceived<TPayload>> writer)
    {
        // Check the size before building a string. Anything with access to the broker can publish to these topics,
        // not only this API, so the payload size cannot be assumed from what the send endpoints accept.
        if (_maxPayloadBytes is { } maxPayloadBytes && message.Payload.Length > maxPayloadBytes)
        {
            logger.LogWarning(
                "Dropped MQTT client event payload of {PayloadBytes} bytes on topic {Topic}: the limit is " +
                "{MaxPayloadBytes} bytes",
                message.Payload.Length, message.Topic, maxPayloadBytes);
            return;
        }

        ApiClientEventReceived<TPayload> received;
        try
        {
            var payloadText = message.ConvertPayloadToString();
            var payload = JsonSerializer.Deserialize<TPayload>(payloadText);
            if (payload is null)
            {
                return;
            }

            received = new ApiClientEventReceived<TPayload>
            {
                Topic = message.Topic,
                Payload = payload
            };
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to deserialize MQTT client event payload");
            return;
        }

        // TryWrite never waits, so a slow reader cannot hold up the MQTT client's message handling (and with it
        // QoS 1 acknowledgements). It returns false when the bounded queue is full, or once the writer is completed.
        if (writer.TryWrite(received))
        {
            return;
        }

        // The reader has fallen too far behind. Dropping events silently would leave the client with gaps it cannot
        // detect (the stream has no event IDs or replay), so end the stream instead. The client reconnects and
        // reloads state. TryComplete returns true only the first time, so this is logged once per stream.
        if (writer.TryComplete())
        {
            logger.LogWarning(
                "Closed MQTT client event stream on topic {Topic}: the reader fell {MaxQueuedEvents} events behind",
                message.Topic, _maxQueuedEvents);
        }
    }

    public async IAsyncEnumerable<ApiClientEventReceived<TPayload>> ReceiveAsync(
        IReadOnlyList<string>? topics = null,
        [EnumeratorCancellation]
        CancellationToken cancellationToken = default)
    {
        IMqttClient? client = null;
        Channel<ApiClientEventReceived<TPayload>>? channel = null;

        if (_maxPayloadBytes is null && MqttApiClientEventReceiverConfiguration.ShouldWarnAboutUnlimitedPayloads())
        {
            logger.LogWarning(
                "ClientEvents:Mqtt:MaxPayloadBytes is zero or negative, so MQTT client event payloads of any size are " +
                "accepted. Leave it unset for the {DefaultMaxPayloadBytes}-byte default, or set a positive limit.",
                MqttApiClientEventReceiverConfiguration.DefaultMaxPayloadBytes);
        }

        if (_maxQueuedEvents is null && MqttApiClientEventReceiverConfiguration.ShouldWarnAboutUnboundedQueues())
        {
            logger.LogWarning(
                "ClientEvents:Mqtt:MaxQueuedEventsPerStream is zero or negative, so a slow event stream can queue " +
                "events without limit. Leave it unset for the default of {DefaultMaxQueuedEventsPerStream}, or set a " +
                "positive limit.",
                MqttApiClientEventReceiverConfiguration.DefaultMaxQueuedEventsPerStream);
        }

        try
        {
            client = await retryWrapperService.RunAsync(
                mqttClientFactory.CreateConnectedClientAsync,
                cancellationToken);

            channel = CreateChannel();

            // MQTT pushes messages on this callback thread, and ReceiveAsync exposes an IAsyncEnumerable to callers.
            // The channel bridges those models: TryWrite enqueues from the handler, and then the ReadAllAsync
            // invocation below dequeues messages into the async iterator that SSE (and any other consumers) pull from.
            client.ApplicationMessageReceivedAsync += args =>
            {
                HandleApplicationMessage(args.ApplicationMessage, channel.Writer);
                return Task.CompletedTask;
            };

            var subscribeBuilder = new MqttClientSubscribeOptionsBuilder();
            if (topics is {Count: > 0})
            {
                foreach (var topic in topics.Where(topic => !string.IsNullOrWhiteSpace(topic)))
                {
                    subscribeBuilder.WithTopicFilter(topic, MqttQualityOfServiceLevel.AtLeastOnce);
                }
            }
            else
            {
                subscribeBuilder.WithTopicFilter("#", MqttQualityOfServiceLevel.AtLeastOnce);
            }

            await retryWrapperService.RunAsync(async token =>
            {
                var subscribeResult = await client.SubscribeAsync(subscribeBuilder.Build(), token);
                if (subscribeResult.Items.Any(item =>
                        item.ResultCode is MqttClientSubscribeResultCode.UnspecifiedError
                            or MqttClientSubscribeResultCode.NotAuthorized
                            or MqttClientSubscribeResultCode.TopicFilterInvalid))
                {
                    throw new ApiClientEventsException("MQTT subscription was not fully granted.")
                    {
                        CouldBeTransient = true,
                        IsHandled = false
                    };
                }
            }, cancellationToken);

            await foreach (var received in channel.Reader.ReadAllAsync(cancellationToken))
            {
                yield return received;
            }
        }
        finally
        {
            channel?.Writer.TryComplete();

            if (client is not null)
            {
                await DisconnectSafelyAsync(client, cancellationToken);
                client.Dispose();
            }
        }
    }
}