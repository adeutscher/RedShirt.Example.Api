namespace RedShirt.Example.Api.Constants;

/// <summary>
///     Realm role names expected in JWT access tokens (Keycloak <c>role</c> claims locally).
///     These are an identity-provider contract; endpoints authorize on
///     <see cref="AuthorizationPolicies" /> / <see cref="AuthorizationPermissions" />.
/// </summary>
public static class AuthorizationRoles
{
    /// <summary>
    ///     Full API access. Local Keycloak treats this as a composite that includes
    ///     <see cref="ApiReadOnly" />; the API map also grants read and write permissions.
    /// </summary>
    public const string ApiUser = "api-user";

    /// <summary>
    ///     Read-only access: <see cref="AuthorizationPermissions.Read" /> only
    ///     (GET endpoints marked with <see cref="Attributes.ApproveReadOnlyAttribute" />).
    /// </summary>
    public const string ApiReadOnly = "api-readonly";
}
