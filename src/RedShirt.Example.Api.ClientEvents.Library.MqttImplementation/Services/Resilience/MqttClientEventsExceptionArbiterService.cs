using RedShirt.Example.Api.ClientEvents.Library.Core.Exceptions;
using RedShirt.Example.Api.ClientEvents.Library.MqttImplementation.Models;
using RedShirt.Example.Api.Common.SecretManagers.Core.Exceptions;
using System.Net.Sockets;
using System.Text.Json;

namespace RedShirt.Example.Api.ClientEvents.Library.MqttImplementation.Services.Resilience;

/// <summary>
///     Classifies MQTT client-events exceptions for retry decisions.
/// </summary>
internal interface IMqttClientEventsExceptionArbiterService
{
    MqttClientEventsExceptionArbiterReport GetReport(Exception exception);
}

/// <summary>
///     MQTT-oriented exception arbiter modelled after the connector / distributed arbiters:
///     known infrastructure failures may be transient; caller cancel and configuration errors are not.
/// </summary>
internal sealed class MqttClientEventsExceptionArbiterService : IMqttClientEventsExceptionArbiterService
{
    private static MqttClientEventsExceptionArbiterReport Fresh(bool isExpected, bool couldBeTransient)
    {
        return new MqttClientEventsExceptionArbiterReport
        {
            AlreadyHandled = false,
            IsExpected = isExpected,
            CouldBeTransient = couldBeTransient
        };
    }

    private static MqttClientEventsExceptionArbiterReport Handled(bool isExpected, bool couldBeTransient)
    {
        return new MqttClientEventsExceptionArbiterReport
        {
            AlreadyHandled = true,
            IsExpected = isExpected,
            CouldBeTransient = couldBeTransient
        };
    }

    private static MqttClientEventsExceptionArbiterReport ClassifyApiClientEventsException(
        ApiClientEventsException exception)
    {
        if (exception.IsHandled)
        {
            return Handled(true, exception.CouldBeTransient);
        }

        if (!exception.CouldBeTransient)
        {
            return Fresh(true, false);
        }

        return Fresh(true, true);
    }

    public MqttClientEventsExceptionArbiterReport GetReport(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        while (exception is AggregateException {InnerExceptions.Count: 1, InnerException: not null} aggregate)
        {
            exception = aggregate.InnerException!;
        }

        return exception switch
        {
            ApiClientEventsException clientEvents => ClassifyApiClientEventsException(clientEvents),
            ApiSecretManagerException secretManager =>
                Handled(true, secretManager is {IsHandled: false, CouldBeTransient: true}),
            SocketException
                or TimeoutException => Fresh(true, true),
            JsonException => Fresh(true, false),
            TaskCanceledException => Fresh(true, true),
            OperationCanceledException => Fresh(true, false),
            ArgumentException => Fresh(true, false),
            _ => Fresh(false, false)
        };
    }
}
