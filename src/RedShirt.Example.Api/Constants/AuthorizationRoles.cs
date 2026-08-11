namespace RedShirt.Example.Api.Constants;

/// <summary>
///     Realm roles expected in JWT access tokens (Keycloak <c>role</c> claims locally).
/// </summary>
public static class AuthorizationRoles
{
    /// <summary>
    ///     Full API access: mutations and any endpoint (subject to authentication).
    /// </summary>
    public const string ApiUser = "api-user";

    /// <summary>
    ///     Read-only access: only endpoints marked with
    ///     <see cref="Attributes.ApproveReadOnlyAttribute" /> (GET requests).
    /// </summary>
    public const string ApiReadOnly = "api-readonly";
}
