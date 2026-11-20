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
    ///     Turns one broker message into a received event and writes it to <paramref name="writer" />. Messages that
    ///     are too large, fail to deserialize, or deserialize to null are dropped with a log entry.
    /// </summary>
    internal async Task HandleApplicationMessageAsync(MqttApplicationMessage message,
        ChannelWriter<ApiClientEventReceived<TPayload>> writer, CancellationToken cancellationToken)
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

        try
        {
            var payloadText = message.ConvertPayloadToString();
            var payload = JsonSerializer.Deserialize<TPayload>(payloadText);
            if (payload is null)
            {
                return;
            }

            // Must forward into the channel: the handler cannot yield return, and the reader is blocked
            // on ReadAllAsync until a message arrives from the broker. WriteAsync is in-process only
            // (an unbounded Channel queue). It does not send MQTT traffic. The broker already delivered
            // the payload before this callback ran.
            await writer.WriteAsync(new ApiClientEventReceived<TPayload>
            {
                Topic = message.Topic,
                Payload = payload
            }, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to deserialize MQTT client event payload");
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

        try
        {
            client = await retryWrapperService.RunAsync(
                mqttClientFactory.CreateConnectedClientAsync,
                cancellationToken);

            channel = Channel.CreateUnbounded<ApiClientEventReceived<TPayload>>(
                new UnboundedChannelOptions {SingleReader = true, SingleWriter = false});

            // MQTT pushes messages on this callback thread; ReceiveAsync exposes an IAsyncEnumerable to callers.
            // The channel bridges those models: WriteAsync enqueues from the handler, and then the ReadAllAsync invocation
            // below dequeues messages into the async iterator that SSE (and any other consumers) pull from.
            client.ApplicationMessageReceivedAsync += args =>
                HandleApplicationMessageAsync(args.ApplicationMessage, channel.Writer, cancellationToken);

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