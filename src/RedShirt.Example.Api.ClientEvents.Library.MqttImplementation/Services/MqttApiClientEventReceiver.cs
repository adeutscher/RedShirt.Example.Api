using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Protocol;
using RedShirt.Example.Api.ClientEvents.Library.Core.Exceptions;
using RedShirt.Example.Api.ClientEvents.Library.Core.Models;
using RedShirt.Example.Api.ClientEvents.Library.Core.Services;
using RedShirt.Example.Api.ClientEvents.Library.MqttImplementation.Factories;
using RedShirt.Example.Api.ClientEvents.Library.MqttImplementation.Services.Resilience;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;

namespace RedShirt.Example.Api.ClientEvents.Library.MqttImplementation.Services;

internal sealed class MqttApiClientEventReceiver<TPayload>(
    IMqttClientFactory mqttClientFactory,
    IMqttClientEventsRetryWrapperService retryWrapperService,
    ILogger<MqttApiClientEventReceiver<TPayload>> logger) : IApiClientEventReceiver<TPayload>
{
    public async IAsyncEnumerable<ApiClientEventReceived<TPayload>> ReceiveAsync(
        IReadOnlyList<string>? topics = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        IMqttClient? client = null;
        Channel<ApiClientEventReceived<TPayload>>? channel = null;

        try
        {
            client = await retryWrapperService.RunAsync(
                mqttClientFactory.CreateConnectedClientAsync,
                cancellationToken);

            channel = Channel.CreateUnbounded<ApiClientEventReceived<TPayload>>(
                new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

            // MQTT pushes messages on this callback thread; ReceiveAsync exposes an IAsyncEnumerable to callers.
            // The channel bridges those models: WriteAsync enqueues from the handler, and then the ReadAllAsync invocation
            // below dequeues messages into the async iterator that SSE (and any other consumers) pull from.
            client.ApplicationMessageReceivedAsync += async args =>
            {
                try
                {
                    var payloadText = args.ApplicationMessage.ConvertPayloadToString();
                    var payload = JsonSerializer.Deserialize<TPayload>(payloadText);
                    if (payload is null)
                    {
                        return;
                    }

                    // Must forward into the channel: the handler cannot yield return, and the reader is blocked
                    // on ReadAllAsync until a message arrives from the broker. WriteAsync is in-process only
                    // (an unbounded Channel queue). It does not send MQTT traffic. The broker already delivered
                    // the payload before this callback ran.
                    await channel.Writer.WriteAsync(new ApiClientEventReceived<TPayload>
                    {
                        Topic = args.ApplicationMessage.Topic,
                        Payload = payload
                    }, cancellationToken);
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Failed to deserialize MQTT client event payload");
                }
            };

            var subscribeBuilder = new MqttClientSubscribeOptionsBuilder();
            if (topics is { Count: > 0 })
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
}
