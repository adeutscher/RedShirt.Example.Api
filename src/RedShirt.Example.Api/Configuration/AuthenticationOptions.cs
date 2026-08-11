namespace RedShirt.Example.Api.Configuration;

/// <summary>
///     JWT bearer authentication settings (typically bound from <c>AUTHENTICATION__*</c> environment variables).
/// </summary>
public sealed class AuthenticationOptions
{
    public const string ConfigurationSectionName = "Authentication";

    /// <summary>
    ///     When <see langword="true" />, JWT authentication and the authenticated-user fallback policy are not registered.
    /// </summary>
    public required bool DisableAuthentication { get; init; }

    /// <summary>
    ///     Expected token issuer / OpenID authority (for example
    ///     <c>http://localhost:9080/realms/example</c>).
    /// </summary>
    public required string? Authority { get; init; }

    /// <summary>
    ///     Optional OpenID discovery URL used when the process cannot reach <see cref="Authority" />
    ///     (for example the API container uses the Keycloak service hostname while tokens still
    ///     carry a localhost issuer).
    /// </summary>
    public required string? MetadataAddress { get; init; }

    /// <summary>
    ///     Expected JWT <c>aud</c> claim (for example <c>example-api</c>).
    /// </summary>
    public required string? Audience { get; init; }

    /// <summary>
    ///     When <see langword="false" />, HTTPS is not required for OpenID metadata (local Keycloak).
    ///     Defaults to <see langword="true" /> when unset.
    /// </summary>
    public required bool? RequireHttpsMetadata { get; init; }

    public bool EffectiveRequireHttpsMetadata => RequireHttpsMetadata ?? true;
}