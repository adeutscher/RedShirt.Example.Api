namespace RedShirt.Example.Api.Authorization.Constants;

/// <summary>
///     Realm role names expected in JWT access tokens (Keycloak <c>role</c> claims locally).
///     These are an identity-provider contract; endpoints authorize on
///     <see cref="BespokeAuthorizationPolicies" /> / <see cref="BespokeAuthorizationPermissions" />.
/// </summary>
public static class BespokeAuthorizationRoles
{
    /// <summary>
    ///     Full API access. Local Keycloak treats this as a composite that includes
    ///     <see cref="ApiReadOnly" />; the API map also grants read and write permissions.
    /// </summary>
    public const string ApiUser = "api-user";

    /// <summary>
    ///     Read-only access: <see cref="BespokeAuthorizationPermissions.Read" /> only
    ///     (GET endpoints marked with <see cref="Attributes.ApproveReadOnlyAttribute" />).
    /// </summary>
    public const string ApiReadOnly = "api-readonly";
}