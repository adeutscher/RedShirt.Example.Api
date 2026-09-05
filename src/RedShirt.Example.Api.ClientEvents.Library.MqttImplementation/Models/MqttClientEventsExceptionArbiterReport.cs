namespace RedShirt.Example.Api.ClientEvents.Library.MqttImplementation.Models;

internal sealed class MqttClientEventsExceptionArbiterReport
{
    public required bool AlreadyHandled { get; init; }

    public required bool IsExpected { get; init; }

    public required bool CouldBeTransient { get; init; }
}
