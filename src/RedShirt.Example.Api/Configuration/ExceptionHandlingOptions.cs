namespace RedShirt.Example.Api.Configuration;

/// <summary>
///     Error response settings (typically bound from <c>EXCEPTION_HANDLING__*</c> environment variables).
/// </summary>
public sealed class ExceptionHandlingOptions
{
    public const string ConfigurationSectionName = "ExceptionHandling";

    /// <summary>
    ///     When <see langword="true" /> and the environment is Development, 502 responses carry the upstream
    ///     exception's message instead of a generic detail. Useful for local debugging of connectors and MQTT.
    ///     Ignored in every other environment, because those messages can contain internal host names, ports, or
    ///     upstream response text.
    /// </summary>
    public bool ExposeUpstreamExceptionMessages { get; init; }

    /// <summary>
    ///     Whether upstream exception messages are actually exposed in <paramref name="environment" />.
    /// </summary>
    public bool EffectiveExposeUpstreamExceptionMessages(IHostEnvironment environment)
    {
        return ExposeUpstreamExceptionMessages && environment.IsDevelopment();
    }
}