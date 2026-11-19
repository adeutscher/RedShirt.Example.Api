namespace RedShirt.Example.Api.Configuration;

/// <summary>
///     JWT bearer authentication settings (typically bound from <c>AUTHENTICATION__*</c> environment variables).
/// </summary>
public sealed class AuthenticationOptions
{
    public const string ConfigurationSectionName = "Authentication";

    /// <summary>
    ///     When <see langword="true" />, JWT authentication and role-based authorization policies are not registered.
    /// </summary>
    public required bool DisableAuthentication { get; init; }

    /// <summary>
    ///     Expected token issuer / OpenID authority (for example
    ///     <c>https://idp.example.com/</c>).
    /// </summary>
    public required string? Authority { get; init; }

    /// <summary>
    ///     Optional OpenID discovery URL used when the process cannot reach <see cref="Authority" />
    ///     (for example an internal service hostname while tokens still carry a public issuer).
    /// </summary>
    public required string? MetadataAddress { get; init; }

    /// <summary>
    ///     Expected JWT <c>aud</c> claim (for example <c>example-api</c>). Required when authentication is enabled:
    ///     startup fails without it, because tokens the authority issues for other clients would otherwise be accepted.
    /// </summary>
    public required string? Audience { get; init; }

    /// <summary>
    ///     When <see langword="false" />, HTTPS is not required for OpenID metadata (local development).
    ///     Defaults to <see langword="true" /> when unset.
    /// </summary>
    public required bool? RequireHttpsMetadata { get; init; }

    public bool EffectiveRequireHttpsMetadata => RequireHttpsMetadata ?? true;
}