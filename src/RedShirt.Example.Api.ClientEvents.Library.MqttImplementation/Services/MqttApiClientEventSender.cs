using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Protocol;
using RedShirt.Example.Api.ClientEvents.Library.Core.Exceptions;
using RedShirt.Example.Api.ClientEvents.Library.Core.Models;
using RedShirt.Example.Api.ClientEvents.Library.Core.Services;
using RedShirt.Example.Api.ClientEvents.Library.MqttImplementation.Factories;
using RedShirt.Example.Api.ClientEvents.Library.MqttImplementation.Services.Resilience;
using System.Text.Json;

namespace RedShirt.Example.Api.ClientEvents.Library.MqttImplementation.Services;

internal sealed class MqttApiClientEventSender<TPayload>(
    IMqttClientFactory mqttClientFactory,
    IMqttClientEventsRetryWrapperService retryWrapperService,
    ILogger<MqttApiClientEventSender<TPayload>> logger) : IApiClientEventSender<TPayload>
{
    public Task SendAsync(ApiClientEventSendRequest<TPayload> request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Topic))
        {
            throw new ApiClientEventsException("An MQTT-shaped topic is required to publish a client event.")
            {
                CouldBeTransient = false,
                IsHandled = false
            };
        }

        return retryWrapperService.RunAsync(async token =>
        {
            var payload = JsonSerializer.Serialize(request.Payload);
            IMqttClient? client = null;

            try
            {
                client = await mqttClientFactory.CreateConnectedClientAsync(token);
                var publishResult = await client.PublishAsync(new MqttApplicationMessageBuilder()
                    .WithTopic(request.Topic)
                    .WithPayload(payload)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                    .Build(), token);

                if (publishResult.ReasonCode is MqttClientPublishReasonCode.UnspecifiedError
                    or MqttClientPublishReasonCode.ImplementationSpecificError)
                {
                    throw new ApiClientEventsException(
                        $"MQTT publish failed with reason code {publishResult.ReasonCode}.")
                    {
                        CouldBeTransient = true,
                        IsHandled = false
                    };
                }
            }
            catch (ApiClientEventsException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Failed to publish MQTT client event to topic {Topic}", request.Topic);
                throw new ApiClientEventsException(exception)
                {
                    CouldBeTransient = true,
                    IsHandled = false
                };
            }
            finally
            {
                if (client is not null)
                {
                    await DisconnectSafelyAsync(client, token);
                    client.Dispose();
                }
            }
        }, cancellationToken);
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
