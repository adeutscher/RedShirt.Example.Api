namespace RedShirt.Example.Api.ClientEvents.Library.Mqtt.Services;

/// <summary>
///     Receive-side settings for <see cref="MqttApiClientEventReceiver{TPayload}" />, bound from the
///     <c>ClientEvents:Mqtt</c> section (environment variables <c>CLIENT_EVENTS__MQTT__*</c>).
/// </summary>
internal sealed class MqttApiClientEventReceiverConfiguration
{
    /// <summary>
    ///     128 KiB. This matches the largest message AWS IoT Core accepts, so anything a managed broker of that kind
    ///     delivers still fits, while a single oversized message cannot take much memory per open stream.
    /// </summary>
    public const int DefaultMaxPayloadBytes = 128 * 1024;

    /// <summary>
    ///     Events that may wait for one slow stream before it is closed. With small events this is a few hundred KiB at
    ///     most. The worst case is this times <see cref="EffectiveMaxPayloadBytes" />, about 32 MiB at the defaults.
    /// </summary>
    public const int DefaultMaxQueuedEventsPerStream = 256;

    private static int _warnedAboutUnboundedQueues;
    private static int _warnedAboutUnlimitedPayloads;

    /// <summary>
    ///     Largest MQTT payload, in bytes, that is deserialized and passed on to receivers. Larger payloads are dropped
    ///     with a warning. When unset, <see cref="DefaultMaxPayloadBytes" /> applies. Zero or negative turns the check
    ///     off.
    /// </summary>
    public int? MaxPayloadBytes { get; init; }

    /// <summary>
    ///     Effective limit, or <see langword="null" /> when the check is off.
    /// </summary>
    public int? EffectiveMaxPayloadBytes => (MaxPayloadBytes ?? DefaultMaxPayloadBytes) switch
    {
        <= 0 => null,
        var limit => limit
    };

    /// <summary>
    ///     Most received events held for one stream while its reader catches up. A stream that falls further behind is
    ///     closed, so the client reconnects and reloads state instead of the queue growing without limit. When unset,
    ///     <see cref="DefaultMaxQueuedEventsPerStream" /> applies. Zero or negative removes the bound.
    /// </summary>
    public int? MaxQueuedEventsPerStream { get; init; }

    /// <summary>
    ///     Effective queue bound, or <see langword="null" /> when the queue is unbounded.
    /// </summary>
    public int? EffectiveMaxQueuedEventsPerStream => (MaxQueuedEventsPerStream ?? DefaultMaxQueuedEventsPerStream) switch
    {
        <= 0 => null,
        var limit => limit
    };

    /// <summary>
    ///     Returns <see langword="true" /> the first time it is called in this process. Used to warn once, not once per
    ///     stream, when the check is off.
    /// </summary>
    internal static bool ShouldWarnAboutUnlimitedPayloads()
    {
        return Interlocked.Exchange(ref _warnedAboutUnlimitedPayloads, 1) == 0;
    }

    /// <summary>
    ///     Returns <see langword="true" /> the first time it is called in this process. Used to warn once, not once per
    ///     stream, when the queue is unbounded.
    /// </summary>
    internal static bool ShouldWarnAboutUnboundedQueues()
    {
        return Interlocked.Exchange(ref _warnedAboutUnboundedQueues, 1) == 0;
    }
}
