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
    ///     Returns <see langword="true" /> the first time it is called in this process. Used to warn once, not once per
    ///     stream, when the check is off.
    /// </summary>
    internal static bool ShouldWarnAboutUnlimitedPayloads()
    {
        return Interlocked.Exchange(ref _warnedAboutUnlimitedPayloads, 1) == 0;
    }
}
